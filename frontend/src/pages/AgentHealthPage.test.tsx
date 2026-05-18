import { cleanup, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AgentHealthPage } from "./AgentHealthPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function healthyResponse() {
  return {
    success: true,
    data: {
      agentReachable: true,
      statusOk: true,
      doctorOk: true,
      activeProfile: "teste",
      agentVersion: "1.2.0-operational-ux",
      configFingerprint: "abc123",
      dbMode: "renewal",
      grfProtected: true,
      lubEditingBlocked: true,
      cacheExists: true,
      cacheMatchesFingerprint: true,
      safety: {
        requireDryRunBeforeApply: true,
        requireDiffBeforeApply: true,
        requireExplicitConfirmation: true,
        backupBeforeApply: true,
        blockOriginalGrfWrite: true,
        blockLubEditing: true,
        invalidateCacheOnPathChange: true,
        cacheMustMatchActiveProfile: true,
        applyBlocked: true,
        rollbackRealBlocked: true,
      },
      doctor: {
        totalChecks: 31,
        passed: 31,
        warnings: 0,
        errors: 0,
        failedChecks: [],
      },
      index: {
        itemsFound: 82848,
        monstersFound: 3681,
        npcsFound: 13860,
        mapsFound: 1100,
        filesScanned: 440970,
        filesParsed: 814,
        filesSkipped: 440156,
        durationMs: 2580,
        generatedAtUtc: "2026-05-17T00:00:00Z",
      },
      validation: {
        totalIssues: 150,
        errorCount: 0,
        warningCount: 150,
        expectedNoiseCount: 150,
        isReadOnlySafe: true,
        isDryRunSafe: true,
        isApplySafe: false,
        topCategories: [
          { code: "MAP_NO_CLIENT_FILES", count: 120 },
          { code: "NPC_NO_MAP", count: 30 },
        ],
      },
      scan: {
        filesVisited: 281,
        filesIndexed: 281,
        filesSkipped: 0,
        directoriesVisited: 57,
        durationMs: 2267,
      },
      warnings: [] as string[],
      errors: [] as string[],
      generatedAtUtc: "2026-05-17T22:00:00Z",
    },
    warnings: [],
    errors: [],
    generatedAt: "2026-05-17T22:00:00Z",
    correlationId: "test-corr",
    operationKind: "ReadOnly",
    readOnlyMode: true,
    durationMs: 100,
  };
}

function offlineResponse() {
  const resp = healthyResponse();
  resp.data.agentReachable = false;
  resp.data.statusOk = false;
  resp.data.doctorOk = false;
  resp.data.errors = ["Agent unreachable"];
  return resp;
}

function warningResponse() {
  const resp = healthyResponse();
  resp.data.doctor.warnings = 1;
  (resp.data.doctor.failedChecks as any) = [
    { check: "cache.fingerprint", severity: "warning", message: "Cache fingerprint mismatch" },
  ];
  resp.data.warnings = ["Cache fingerprint mismatch"];
  return resp;
}

function staleCacheResponse() {
  const resp = warningResponse();
  resp.data.cacheMatchesFingerprint = false;
  resp.data.index = null as any;
  resp.data.scan = null as any;
  resp.data.warnings = [
    "entities_index.json is stale for the active profile/fingerprint.",
    "project_index.json is stale for the active profile/fingerprint.",
  ];
  return resp;
}

function mockFetch(response: ReturnType<typeof healthyResponse>) {
  globalThis.fetch = vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
    if (url.includes("/api/agent/health")) {
      return new Response(JSON.stringify(response), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }
    // Default stub for other API calls (status, health, etc.)
    return new Response(JSON.stringify({ status: "ok" }), {
      status: 200,
      headers: { "content-type": "application/json" },
    });
  }) as typeof fetch;
}

describe("AgentHealthPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("renders agent status badges when healthy", async () => {
    mockFetch(healthyResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText("Agent Online")).toBeInTheDocument();
    expect(await screen.findByText("Status OK")).toBeInTheDocument();
    expect(await screen.findByText("GRF Protegido")).toBeInTheDocument();
    expect(await screen.findByText(".lub Bloqueado")).toBeInTheDocument();
  });

  it("renders doctor check summary", async () => {
    mockFetch(healthyResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText("Doctor 31/31")).toBeInTheDocument();
    expect(await screen.findByText("Todos os checks passaram com sucesso.")).toBeInTheDocument();
  });

  it("renders validation category codes", async () => {
    mockFetch(healthyResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText("MAP_NO_CLIENT_FILES")).toBeInTheDocument();
    expect(await screen.findByText("NPC_NO_MAP")).toBeInTheDocument();
  });

  it("renders error state when agent is offline", async () => {
    mockFetch(offlineResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText("Agent Offline")).toBeInTheDocument();
    expect(await screen.findByText("Status Falha")).toBeInTheDocument();
  });

  it("renders warning state from agent doctor", async () => {
    mockFetch(warningResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText("cache.fingerprint")).toBeInTheDocument();
    expect((await screen.findAllByText("Cache fingerprint mismatch")).length).toBeGreaterThan(0);
  });

  it("renders stale cache warnings without trusted counts", async () => {
    mockFetch(staleCacheResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText(/Cache do Agent requer atencao/i)).toBeInTheDocument();
    expect(await screen.findByText(/Indice de entidades nao disponivel/i)).toBeInTheDocument();
  });

  it("never renders apply or rollback buttons", async () => {
    mockFetch(healthyResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(await screen.findByText("Agent Online")).toBeInTheDocument();

    expect(screen.queryByText("Apply")).not.toBeInTheDocument();
    expect(screen.queryByText("Rollback")).not.toBeInTheDocument();
    expect(screen.queryByText("Aplicar")).not.toBeInTheDocument();
    expect(screen.queryByText("Reverter")).not.toBeInTheDocument();
  });

  it("renders read-only notice and blocked operation badges", async () => {
    mockFetch(healthyResponse());
    renderWithProviders(<AgentHealthPage />);

    expect(
      await screen.findByText(/Esta integracao e estritamente read-only/i),
    ).toBeInTheDocument();
    expect(await screen.findByText("Apply Blocked")).toBeInTheDocument();
    expect(await screen.findByText("Rollback Real Blocked")).toBeInTheDocument();
  });
});
