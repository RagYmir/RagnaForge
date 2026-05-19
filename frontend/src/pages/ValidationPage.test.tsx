import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ValidationPage } from "./ValidationPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function statusEnvelope() {
  return {
    success: true,
    data: {
      service: "RagnaForge API",
      mode: "read-only-dry-run-diff-preview",
      workspaceRoot: "C:/Users/Allis/Desktop/Ragna_Forge",
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
    correlationId: "cid-status",
    operationKind: "ReadOnly",
    readOnlyMode: true,
    durationMs: 1,
  };
}

function configEnvelope() {
  return {
    success: true,
    data: {
      manifestPath: "data/manifests/repositories.local.json",
      validation: { isValid: true, issues: [] },
      manifest: {
        schemaVersion: "1",
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
        paths: {},
        episodeProfile: {},
        isProgressive: true,
        clientDateStatus: "Unknown",
        notes: [],
      },
    },
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-config",
    operationKind: "ReadOnly",
    readOnlyMode: true,
    durationMs: 1,
  };
}

function discoverEnvelope() {
  return {
    success: true,
    data: {
      patch: { exists: true },
    },
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-discover",
    operationKind: "ReadOnly",
    readOnlyMode: true,
    durationMs: 1,
  };
}

function seedHistory() {
  localStorage.setItem(
    "ragnaforge-admin-ui.pipeline-history.v1",
    JSON.stringify([
      {
        id: "history-item",
        category: "items",
        kind: "dry-run",
        createdAt: "2026-05-12T10:00:00Z",
        payload: {
          configPath: "data/manifests/repositories.local.json",
          resourceName: "RF_HISTORY_ITEM",
        },
        responseData: {
          canApply: false,
          applyReadiness: "Blocked by client-side",
          clientSidePlan: {
            canApply: false,
            clientSideMode: "Hybrid",
            blockReasons: ["itemInfo bytecode blocked"],
            bytecodeBlockedFiles: ["data\\luafiles514\\lua files\\datainfo\\itemInfo.lub"],
          },
          assetLookup: {
            source: "LocalIndex",
            matches: [],
          },
        },
        success: true,
        warningsCount: 1,
        errorsCount: 1,
        summary: "History item",
        correlationId: "cid-item",
        readiness: "Blocked",
        canApply: false,
        diffFileCount: 1,
      },
      {
        id: "history-map",
        category: "maps",
        kind: "dry-run",
        createdAt: "2026-05-12T10:01:00Z",
        payload: {
          configPath: "data/manifests/repositories.local.json",
          mapName: "rf_missing_map",
        },
        responseData: {
          canApply: false,
          applyReadiness: "Missing dependencies",
          mapCachePlan: {
            requiresRebuild: true,
          },
          assetPlans: [
            {
              referencePath: "data\\texture\\rf_missing.bmp",
              sourceKind: "Missing",
              resolved: false,
            },
            {
              referencePath: "data\\model\\rf_ambiguous.rsm",
              sourceKind: "Ambiguous",
              resolved: true,
            },
          ],
        },
        success: true,
        warningsCount: 1,
        errorsCount: 0,
        summary: "History map",
        correlationId: "cid-map",
        readiness: "Warn",
        canApply: false,
        diffFileCount: 2,
      },
    ]),
  );
}

describe("ValidationPage", () => {
  beforeEach(() => {
    installConnection();
    seedHistory();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("mostra categorias e preview passivo consolidado a partir do historico local", async () => {
    globalThis.fetch = vi.fn(async (input) => {
      const url = String(input);
      const payload = url.includes("/api/status")
        ? statusEnvelope()
        : url.includes("/api/config/validate")
          ? configEnvelope()
          : discoverEnvelope();

      return new Response(JSON.stringify(payload), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }) as typeof fetch;

    renderWithProviders(<ValidationPage />);

    expect((await screen.findAllByText(/Unknown Item\/Apple/i)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Bytecode/i).length).toBeGreaterThan(0);

    await userEvent.click(screen.getByRole("tab", { name: "Resources" }));

    expect(await screen.findByText(/Preview passivo consolidado/i)).toBeInTheDocument();
    expect(screen.getAllByText(/rf_missing\.bmp/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/rf_ambiguous\.rsm/i).length).toBeGreaterThan(0);
    expect(
      screen.getAllByText(/Preview visual read-only seguro via API\./i).length,
    ).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: /apply/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /rollback/i })).not.toBeInTheDocument();
  });
});
