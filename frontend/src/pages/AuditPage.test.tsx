import { screen, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest";
import { AuditPage } from "./AuditPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

describe("AuditPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("mostra comparacao e historico local read-only", async () => {
    localStorage.setItem(
      "ragnaforge-admin-ui.pipeline-history.v1",
      JSON.stringify([
        {
          id: "left",
          category: "items",
          kind: "dry-run",
          createdAt: new Date().toISOString(),
          payload: { aegisName: "ITEM_A" },
          responseData: { applyReadiness: "Ready", canApply: true },
          success: true,
          warningsCount: 0,
          errorsCount: 0,
          summary: "ITEM_A",
          correlationId: "cid-left",
          readiness: "Ready",
          canApply: true,
          diffFileCount: 2,
        },
        {
          id: "right",
          category: "items",
          kind: "dry-run",
          createdAt: new Date().toISOString(),
          payload: { aegisName: "ITEM_B" },
          responseData: { applyReadiness: "Blocked", canApply: false },
          success: true,
          warningsCount: 1,
          errorsCount: 1,
          summary: "ITEM_B",
          correlationId: "cid-right",
          readiness: "Blocked",
          canApply: false,
          diffFileCount: 4,
        },
      ]),
    );

    globalThis.fetch = vi.fn(async () =>
      new Response(
        JSON.stringify({
          success: true,
          data: {
            service: "RagnaForge API",
            mode: "read-only-dry-run-diff-preview",
            workspaceRoot: "C:/Users/Allis/Documents/New project",
            readOnlyMode: true,
            applyEndpointsEnabled: false,
            rollbackEndpointsEnabled: false,
            requireApiKey: true,
            apiKeyHeaderName: "X-RagnaForge-Api-Key",
            maxRequestBodyBytes: 1048576,
            maxGrfContainersPerRequest: 50,
            maxDiffHunksPerResponse: 500,
            generatedAtUtc: new Date().toISOString(),
            disabledWriteOperations: ["Apply", "Rollback"],
            capabilities: [],
          },
          warnings: [],
          errors: [],
          generatedAt: new Date().toISOString(),
          correlationId: "cid-audit",
          operationKind: "ReadOnly",
          readOnlyMode: true,
          durationMs: 1,
        }),
        { status: 200, headers: { "content-type": "application/json" } },
      ),
    ) as typeof fetch;

    renderWithProviders(<AuditPage />);

    expect(await screen.findByText(/Historico e relatorios/i)).toBeInTheDocument();
    expect(screen.getByText(/Ultimos resultados locais/i)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("tab", { name: "Comparacao" }));
    expect(screen.getByText(/Comparacao entre dry-runs/i)).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Readiness" })).toBeInTheDocument();
    expect(screen.getAllByText("Ready").length).toBeGreaterThan(0);
    expect(screen.getByRole("cell", { name: "Blocked" })).toBeInTheDocument();
  });
});
