import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NpcsPage } from "./NpcsPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function envelope(data: Record<string, unknown>) {
  return {
    success: true,
    data,
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-npc",
    operationKind: "DryRun",
    readOnlyMode: true,
    durationMs: 3,
  };
}

describe("NpcsPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("renderiza ClientIdentityPlan e nao mostra botoes de apply/rollback", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response(
        JSON.stringify(
          envelope({
            canApply: true,
            applyReadiness: "Ready",
            serverCanApply: true,
            canApplyClientIdentity: true,
            detectionSource: "Patch",
            spriteResolution: {
              resolved: true,
              source: "Patch",
            },
            requiredClientFiles: ["jobname.lua", "npcidentity.lua"],
            proposedClientRegistration: ["JT_SAMPLE_NPC"],
            postWriteValidationPlan: ["Validar jobname.lua textual"],
            clientIdentityPlan: {
              required: true,
              canApply: true,
              applyReadiness: "Ready",
              spriteResolved: true,
              spriteName: "sample_npc",
              spriteSource: "Patch",
              spritePath: "data/sprite/npc/sample_npc.spr",
              filesDetected: [
                {
                  logicalName: "jobname",
                  path: "data/luafiles514/lua files/datainfo/jobname.lua",
                  format: "TextLua",
                  exists: true,
                  selected: true,
                },
              ],
              fileFormats: ["TextLua"],
              proposedRegistrations: ["JT_SAMPLE_NPC"],
              existingRegistrations: [],
              validationWarnings: ["jobname textual pronto para diff-preview"],
              bytecodeBlockedFiles: [],
            },
          }),
        ),
        {
          status: 200,
          headers: { "content-type": "application/json" },
        },
      ),
    ) as typeof fetch;

    renderWithProviders(<NpcsPage />);

    await userEvent.type(screen.getByLabelText("Name"), "sample_npc");
    await userEvent.type(screen.getByLabelText("Map"), "prontera");
    await userEvent.type(screen.getByLabelText("Sprite"), "sample_npc");
    await userEvent.click(screen.getByRole("button", { name: /gerar dry-run/i }));

    expect(await screen.findByText("ClientIdentityPlan")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Sprite resolution" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /apply/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /rollback/i })).not.toBeInTheDocument();
  });
});
