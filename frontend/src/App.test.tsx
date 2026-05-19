import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { ApiConfigProvider } from "./features/connection/ApiConfigContext";

const originalFetch = globalThis.fetch;

function statusEnvelope() {
  return {
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
        paths: {
          rAthenaPath: "E:/Ragnarok/Testes/rAthena_teste",
          patchPath: "E:/Ragnarok/Testes/Patch_teste",
          grfEditorPath: "C:/Program Files (x86)/GRF Editor",
        },
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

function renderApp(route = "/") {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={client}>
      <ApiConfigProvider>
        <MemoryRouter initialEntries={[route]}>
          <App />
        </MemoryRouter>
      </ApiConfigProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  localStorage.setItem(
    "ragnaforge-admin-ui.connection",
    JSON.stringify({
      baseUrl: "http://127.0.0.1:5099",
      apiKey: "local-key",
    }),
  );
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

describe("App", () => {
  it("menu tem paginas esperadas e nao expoe apply", async () => {
    globalThis.fetch = vi.fn(async (input) => {
      const url = String(input);
      const payload = url.includes("/api/status")
        ? statusEnvelope()
        : configEnvelope();

      return new Response(JSON.stringify(payload), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }) as typeof fetch;

    renderApp("/");

    expect(await screen.findByRole("link", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Itens" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Validacao" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Historico/Relatorios" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Pipeline API" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /apply/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /apply/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /rollback/i })).not.toBeInTheDocument();
  });

  it("rotas apply/rollback agora caem em pagina nao encontrada", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response(JSON.stringify(statusEnvelope()), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    ) as typeof fetch;

    renderApp("/itens/apply");

    expect(await screen.findByText(/Pagina nao encontrada/i)).toBeInTheDocument();
  });

  it("tela inicial mostra ReadOnlyMode mesmo se a primeira consulta falhar", async () => {
    let statusCalls = 0;
    globalThis.fetch = vi.fn(async (input) => {
      const url = String(input);

      if (url.includes("/api/status")) {
        statusCalls += 1;
        if (statusCalls === 1) {
          return new Response(
            JSON.stringify({
              status: 401,
              title: "Unauthorized",
              detail: "API key invalida.",
              errorCode: "AUTH_INVALID",
              correlationId: "cid-401",
              path: "/api/status",
              timestamp: "2026-05-11T10:00:00Z",
            }),
            {
              status: 401,
              headers: { "content-type": "application/problem+json" },
            },
          );
        }

        return new Response(JSON.stringify(statusEnvelope()), {
          status: 200,
          headers: { "content-type": "application/json" },
        });
      }

      return new Response(JSON.stringify(configEnvelope()), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }) as typeof fetch;

    renderApp("/");

    await waitFor(() => {
      expect(screen.getByText(/ReadOnlyMode = true/i)).toBeInTheDocument();
    });

    expect(screen.getByText(/Config validation/i)).toBeInTheDocument();
  });

  it("aba de seguranca reforca que apply e rollback nao existem na interface", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response(JSON.stringify(statusEnvelope()), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    ) as typeof fetch;

    renderApp("/seguranca");

    expect(
      await screen.findByText(
        /Apply e rollback nao existem nesta interface nesta fase\./i,
      ),
    ).toBeInTheDocument();
  });
});
