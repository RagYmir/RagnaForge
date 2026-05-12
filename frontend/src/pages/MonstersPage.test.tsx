import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MonstersPage } from "./MonstersPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function envelope(data: Record<string, unknown>) {
  return {
    success: true,
    data,
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-monster",
    operationKind: "DryRun",
    readOnlyMode: true,
    durationMs: 4,
  };
}

describe("MonstersPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("renderiza grids de drops, skills e spawns", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response(
        JSON.stringify(
          envelope({
            canApply: true,
            applyReadiness: "Ready",
            drops: [{ itemReference: "Jellopy", chance: 1000, quantity: 1, isMvp: false }],
            skills: [{ skillId: "MG_FIREBOLT", skillLevel: 5, state: "idle", supported: true }],
            spawns: [{ mapName: "prontera", amount: 5, respawnMilliseconds: 60000 }],
            postWriteValidationPlan: ["Validar mob_db staging"],
          }),
        ),
        {
          status: 200,
          headers: { "content-type": "application/json" },
        },
      ),
    ) as typeof fetch;

    renderWithProviders(<MonstersPage />);

    await userEvent.type(screen.getByLabelText("AegisName"), "SAMPLE_MONSTER");
    await userEvent.type(screen.getByLabelText("Name"), "Sample Monster");
    await userEvent.type(screen.getByLabelText("Sprite"), "sample_monster");
    await userEvent.click(screen.getByRole("button", { name: /gerar dry-run/i }));
    await userEvent.click(await screen.findByRole("tab", { name: "Drops" }));

    expect(screen.getByText(/Jellopy/i)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("tab", { name: "Skills" }));
    expect(screen.getByText(/MG_FIREBOLT/i)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("tab", { name: "Spawns" }));
    expect(screen.getByText(/prontera/i)).toBeInTheDocument();
  });
});
