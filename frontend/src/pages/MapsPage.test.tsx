import { screen, cleanup } from "@testing-library/react";
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest";
import userEvent from "@testing-library/user-event";
import { MapsPage } from "./MapsPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function envelope(data: Record<string, unknown>) {
  return {
    success: true,
    data,
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-map",
    operationKind: "DryRun",
    readOnlyMode: true,
    durationMs: 5,
  };
}

describe("MapsPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("renderiza DependencyTree para dependencias de mapa", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response(
        JSON.stringify(
          envelope({
            canApply: false,
            applyReadiness: "Blocked",
            needsCopy: true,
            assetPlans: [
              {
                targetPath: "data/map/newmap.rsw",
                relativePath: "newmap.rsw",
                category: "map",
                required: true,
              },
              {
                targetPath: "conf/maps_athena.conf",
                category: "config",
              },
              {
                targetPath: "data/model/tree.rsm",
                relativePath: "data/model/tree.rsm",
                sourceKind: "AmbiguousGrf",
              },
            ],
            dependencyScan: {
              referencedAssets: [
                {
                  referencePath: "data/map/newmap.rsw",
                  category: "map",
                  resolved: true,
                },
                {
                  referencePath: "data/texture/rock.bmp",
                  category: "texture",
                  resolved: false,
                },
              ],
            },
            mapCachePlan: {
              toolDetected: true,
              cachePath: "data/map_cache.dat",
            },
          }),
        ),
        {
          status: 200,
          headers: { "content-type": "application/json" },
        },
      ),
    ) as typeof fetch;

    renderWithProviders(<MapsPage />);

    await userEvent.type(screen.getByLabelText("MapName"), "newmap");
    await userEvent.click(screen.getByRole("button", { name: /gerar dry-run/i }));
    await userEvent.click(await screen.findByRole("tab", { name: "Dependencies" }));

    expect((await screen.findAllByText(/data\/texture\/rock\.bmp/i)).length).toBeGreaterThan(0);
  });
});
