import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PassiveAssetPreviewPanel } from "./PassiveAssetPreviewPanel";

import { renderWithProviders, installConnection } from "../test/renderWithProviders";
import { beforeEach } from "vitest";

describe("PassiveAssetPreviewPanel", () => {
  beforeEach(() => {
    installConnection();
  });

  it("mostra status read-only sem sugerir extracao ou copia", () => {
    renderWithProviders(
      <PassiveAssetPreviewPanel
        title="Assets"
        items={[
          {
            key: "asset-1",
            name: "rf_headgear.bmp",
            path: "data\\texture\\rf_headgear.bmp",
            expectedPath: "data\\texture\\rf_headgear.bmp",
            category: "Item visuals",
            type: ".bmp",
            origin: "Patch",
            provenance: "ReadOnly",
            status: "resolved",
            note: "Encontrado sem extracao.",
          },
        ]}
      />,
    );

    expect(screen.getAllByText(/rf_headgear\.bmp/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/Preview visual real pendente de endpoint seguro de leitura\./i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /extrair/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /copiar/i })).not.toBeInTheDocument();
  });
});
