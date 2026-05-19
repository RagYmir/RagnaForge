import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiPipelineWorkspacePage } from "./ApiPipelineWorkspacePage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function envelope<T>(data: T, operationKind = "ReadOnly") {
  return {
    success: true,
    data,
    warnings: [],
    errors: [],
    generatedAt: "2026-05-18T10:00:00Z",
    correlationId: `cid-${operationKind}`,
    operationKind,
    readOnlyMode: true,
    durationMs: 1,
  };
}

function installPipelineFetch() {
  globalThis.fetch = vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;

    if (url.includes("/api/pipeline/status")) {
      return new Response(JSON.stringify(envelope({
        apiReadOnly: true,
        dryRunAvailable: true,
        diffPreviewAvailable: true,
        applyAvailable: false,
        rollbackRealAvailable: false,
        agentHealthSummary: null,
        safeForReadOnlyWork: true,
        safeForDryRun: true,
        safeForApply: false,
        externalDataIssueCount: 1084,
        currentKnownLimitations: ["Apply real bloqueado."],
      })), { status: 200, headers: { "content-type": "application/json" } });
    }

    if (url.includes("/api/pipeline/issues")) {
      return new Response(JSON.stringify(envelope({
        readOnly: true,
        safeForReadOnlyWork: true,
        safeForDryRun: true,
        safeForApply: false,
        summary: {
          total: 1084,
          errors: 1,
          warnings: 1083,
          issues: [],
          externalDataCount: 1084,
          applyBlockersCount: 1,
          dryRunBlockersCount: 0,
        },
        issues: [],
        warnings: [],
        errors: [],
      })), { status: 200, headers: { "content-type": "application/json" } });
    }

    if (url.includes("/api/pipeline/reports")) {
      return new Response(JSON.stringify(envelope([
        {
          id: "op-readonly",
          title: "Pipeline read-only report",
          entityType: "system",
          generatedAtUtc: "2026-05-18T10:00:00Z",
          sizeBytes: 2048,
        },
      ])), { status: 200, headers: { "content-type": "application/json" } });
    }

    if (url.includes("/api/pipeline/plan")) {
      return new Response(JSON.stringify(envelope({
        operationId: "op-safe",
        readOnly: true,
        entityType: "item",
        dependencySummary: {
          serverDb: [{ name: "item_db.yml", type: "YAML", status: "NotChecked", expectedPath: "db/item_db.yml", source: "rAthena", notes: "read-only" }],
          clientDb: [],
          scripts: [],
          assets: [{ name: "item.spr", type: "Sprite", status: "Placeholder", expectedPath: "sprite/item.spr", source: "GRF/Patch", notes: "placeholder" }],
        },
        validationSummary: {
          total: 0,
          errors: 0,
          warnings: 0,
          issues: [],
          externalDataCount: 0,
          applyBlockersCount: 0,
          dryRunBlockersCount: 0,
        },
        plannedSteps: [{ name: "Server DB Item Proposal", action: "Append preview", target: "RF_PIPELINE_ITEM", status: "Pending", reason: null }],
        blockedSteps: [{ name: "Persistent Content Apply", action: "Write content", target: "rAthena/Patch", status: "Blocked", reason: "Apply real bloqueado" }],
        warnings: [],
        errors: [],
        readiness: { canInspect: true, canDryRun: true, canDiffPreview: true, canApply: false },
        links: { dryRun: "/api/pipeline/dry-run", diffPreview: "/api/pipeline/diff-preview", report: "/api/pipeline/reports/op-safe" },
      })), { status: 200, headers: { "content-type": "application/json" } });
    }

    if (url.includes("/api/pipeline/dry-run")) {
      return new Response(JSON.stringify(envelope({
        operationId: "op-safe",
        noPersistentWrites: true,
        dryRunReport: { canApply: false },
        generatedFilesPreview: ["rAthena/db/import/item_db.yml (append preview)"],
        warnings: [],
        errors: [],
        safeForApply: false,
      }, "DryRun")), { status: 200, headers: { "content-type": "application/json" } });
    }

    if (url.includes("/api/pipeline/diff-preview")) {
      return new Response(JSON.stringify(envelope({
        operationId: "op-safe",
        noPersistentWrites: true,
        diffByFile: [{ targetPath: "db/import/item_db.yml", changeKind: "Preview", exists: true, unifiedDiff: "+ RF_PIPELINE_ITEM", preview: "+ RF_PIPELINE_ITEM" }],
        additions: 1,
        modifications: 0,
        deletions: 0,
        riskLevel: "Low",
      }, "DiffPreview")), { status: 200, headers: { "content-type": "application/json" } });
    }

    return new Response(JSON.stringify(envelope({})), { status: 200, headers: { "content-type": "application/json" } });
  }) as typeof fetch;
}

describe("ApiPipelineWorkspacePage", () => {
  beforeEach(() => {
    installConnection();
    installPipelineFetch();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("renders read-only pipeline status and issues", async () => {
    renderWithProviders(<ApiPipelineWorkspacePage />);

    expect(await screen.findByText("API Pipeline Workspace")).toBeInTheDocument();
    expect(await screen.findByText("ReadOnly = true")).toBeInTheDocument();
    expect(await screen.findByText("Apply bloqueado")).toBeInTheDocument();
    expect(await screen.findByText("Rollback real bloqueado")).toBeInTheDocument();
    expect(await screen.findByText("External-data")).toBeInTheDocument();
    expect(await screen.findByText("Pipeline read-only report")).toBeInTheDocument();
  });

  it("runs plan, dry-run and diff-preview without destructive buttons", async () => {
    renderWithProviders(<ApiPipelineWorkspacePage />);

    await userEvent.click(await screen.findByRole("button", { name: /gerar plano/i }));
    expect(await screen.findByText("Dependency Resolver Summary")).toBeInTheDocument();
    expect(await screen.findByText("Server DB Item Proposal")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /executar dry-run seguro/i }));
    expect(await screen.findByText("NoPersistentWrites = true")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /gerar diff-preview/i }));
    expect(await screen.findByText("Diff Preview")).toBeInTheDocument();
    expect(await screen.findByText("Additions = 1")).toBeInTheDocument();

    expect(screen.queryByRole("button", { name: /^apply$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^rollback$/i })).not.toBeInTheDocument();

    await waitFor(() => {
      const calls = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls;
      expect(calls.some(([url]) => String(url).includes("/apply"))).toBe(false);
      expect(calls.some(([url]) => String(url).includes("/rollback"))).toBe(false);
    });
  });
});
