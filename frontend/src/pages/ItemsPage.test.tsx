import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ItemsPage } from "./ItemsPage";
import { installConnection, renderWithProviders } from "../test/renderWithProviders";

const originalFetch = globalThis.fetch;

function envelope() {
  return {
    success: true,
    data: {
      canApply: true,
      applyReadiness: "Ready",
      clientSidePlan: {
        canApply: true,
        clientSideMode: "ItemInfo",
        applyReadiness: "Ready",
        proposedRegistrations: ["RF_ETC_SIMPLE"],
        existingRegistrations: [],
        bytecodeBlockedFiles: [],
      },
      assetLookup: {
        source: "Patch",
        totalMatches: 1,
        matches: [{ relativePath: "data/texture/rf_item.bmp", extension: ".bmp", containerPath: "Patch" }],
      },
    },
    warnings: [],
    errors: [],
    generatedAt: new Date().toISOString(),
    correlationId: "cid-item",
    operationKind: "DryRun",
    readOnlyMode: true,
    durationMs: 3,
  };
}

describe("ItemsPage", () => {
  beforeEach(() => {
    installConnection();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it("preset preenche formulario sem chamar API e historico pode ser reutilizado", async () => {
    globalThis.fetch = vi.fn(async () =>
      new Response(JSON.stringify(envelope()), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    ) as typeof fetch;

    renderWithProviders(<ItemsPage />);

    await userEvent.click(screen.getAllByRole("button", { name: /usar preset/i })[0]);

    expect(screen.getByLabelText("AegisName")).toHaveValue("RF_ETC_SIMPLE");
    expect(globalThis.fetch).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: /gerar dry-run/i }));
    await userEvent.click(screen.getByRole("tab", { name: "Client-side" }));
    expect(await screen.findByText(/ClientSidePlan/i)).toBeInTheDocument();

    const historyRaw = localStorage.getItem("ragnaforge-admin-ui.pipeline-history.v1");
    expect(historyRaw).toContain("RF_ETC_SIMPLE");

    await userEvent.clear(screen.getByLabelText("AegisName"));
    await userEvent.type(screen.getByLabelText("AegisName"), "TEMP_ITEM");
    await userEvent.click(screen.getByRole("button", { name: /reenviar ao formulario/i }));

    expect(screen.getByLabelText("AegisName")).toHaveValue("RF_ETC_SIMPLE");
    expect(globalThis.fetch).toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: /limpar historico/i }));
    await waitFor(() => {
      expect(localStorage.getItem("ragnaforge-admin-ui.pipeline-history.v1")).toBe("[]");
    });
  });
});
