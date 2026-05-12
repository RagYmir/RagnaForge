import { screen, cleanup } from "@testing-library/react";
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest";
import userEvent from "@testing-library/user-event";
import { GrfPage } from "./GrfPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function envelope(data: Record<string, unknown>, operationKind = "ReadOnly") {
  return {
    success: true,
    data,
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-grf",
    operationKind,
    readOnlyMode: true,
    durationMs: 4,
  };
}

describe("GrfPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("mostra filtros por extensao para assets", async () => {
    globalThis.fetch = vi.fn(async (input) => {
      const url = String(input);
      if (url.includes("/api/grf/index")) {
        return new Response(
          JSON.stringify(
            envelope({
              index: {
                containers: [
                  {
                    name: "data.grf",
                    extension: ".grf",
                    fullPath: "E:/Ragnarok/data.grf",
                  },
                ],
              },
            }),
          ),
          { status: 200, headers: { "content-type": "application/json" } },
        );
      }

      return new Response(
        JSON.stringify(
          envelope({
            index: {
              extensionCounts: [{ extension: ".spr" }, { extension: ".rsw" }],
              entries: [
                {
                  relativePath: "data/sprite/npc/test.spr",
                  fileName: "test.spr",
                  extension: ".spr",
                },
                {
                  relativePath: "data/map/prontera.rsw",
                  fileName: "prontera.rsw",
                  extension: ".rsw",
                },
              ],
            },
          }),
        ),
        { status: 200, headers: { "content-type": "application/json" } },
      );
    }) as typeof fetch;

    renderWithProviders(<GrfPage />);

    await userEvent.click(screen.getByRole("button", { name: /indexar grfs/i }));
    const inspectButton = screen.getByRole("button", { name: /inspecionar container/i });
    await userEvent.click(inspectButton);
    await userEvent.click(await screen.findByRole("tab", { name: "Assets" }));

    expect(screen.getByRole("button", { name: ".spr" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: ".rsw" })).toBeInTheDocument();
  });
});
