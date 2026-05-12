import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiClient, ApiClientError } from "./client";

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

describe("ApiClient", () => {
  it("adiciona API key e correlation id", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          data: { readOnlyMode: true },
          warnings: [],
          errors: [],
          generatedAt: new Date().toISOString(),
          correlationId: "server-correlation",
          operationKind: "ReadOnly",
          readOnlyMode: true,
          durationMs: 1,
        }),
        {
          status: 200,
          headers: { "content-type": "application/json" },
        },
      ),
    );

    globalThis.fetch = fetchMock as typeof fetch;

    const client = new ApiClient(() => ({
      baseUrl: "http://127.0.0.1:5099",
      apiKey: "local-key",
    }));

    await client.status();

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const headers = new Headers(init.headers);

    expect(headers.get("X-RagnaForge-Api-Key")).toBe("local-key");
    expect(headers.get("X-Correlation-Id")).toBeTruthy();
  });

  it("interpreta ApiResponse com sucesso", async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          data: { ok: true },
          warnings: ["warn-1"],
          errors: [],
          generatedAt: "2026-05-11T10:00:00Z",
          correlationId: "cid-1",
          operationKind: "DryRun",
          readOnlyMode: true,
          durationMs: 5,
        }),
        {
          status: 200,
          headers: { "content-type": "application/json" },
        },
      ),
    ) as typeof fetch;

    const client = new ApiClient(() => ({
      baseUrl: "http://127.0.0.1:5099",
      apiKey: "local-key",
    }));

    const response = await client.itemDryRun({
      aegisName: "TEST_ITEM",
      displayName: "Test Item",
      type: "Etc",
      buy: 0,
      sell: 0,
      weight: 0,
      identifiedDescriptionLines: [],
    });

    expect(response.success).toBe(true);
    expect((response.data as { ok: boolean }).ok).toBe(true);
    expect(response.warnings).toEqual(["warn-1"]);
    expect(response.correlationId).toBe("cid-1");
  });

  it("interpreta ProblemDetails corretamente", async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 401,
          title: "Unauthorized",
          detail: "API key ausente.",
          errorCode: "AUTH_REQUIRED",
          correlationId: "cid-problem",
          path: "/api/status",
          timestamp: "2026-05-11T10:00:00Z",
        }),
        {
          status: 401,
          headers: { "content-type": "application/problem+json" },
        },
      ),
    ) as typeof fetch;

    const client = new ApiClient(() => ({
      baseUrl: "http://127.0.0.1:5099",
      apiKey: "",
    }));

    try {
      await client.status();
      throw new Error("Expected ApiClientError");
    } catch (error) {
      const apiError = error as ApiClientError;
      expect(apiError).toBeInstanceOf(ApiClientError);
      expect(apiError.problem?.errorCode).toBe("AUTH_REQUIRED");
      expect(apiError.problem?.correlationId).toBe("cid-problem");
      expect(apiError.problem?.detail).toContain("API key");
    }
  });
});
