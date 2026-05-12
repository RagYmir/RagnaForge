import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DependencyTree } from "./DependencyTree";

describe("DependencyTree", () => {
  it("mostra estados e origem das dependencias", () => {
    render(
      <DependencyTree
        title="Dependencies"
        groups={[
          {
            title: "Assets",
            items: [
              { label: "resolved.asset", status: "resolved", origin: "Patch" },
              { label: "missing.asset", status: "missing", origin: "Unknown" },
              { label: "ambiguous.asset", status: "ambiguous", origin: "LiveScan" },
              { label: "blocked.asset", status: "blocked", origin: "Bytecode" },
            ],
          },
        ]}
      />,
    );

    expect(screen.getByText("resolved")).toBeInTheDocument();
    expect(screen.getByText("missing")).toBeInTheDocument();
    expect(screen.getByText("ambiguous")).toBeInTheDocument();
    expect(screen.getByText("blocked")).toBeInTheDocument();
    expect(screen.getByText("Patch")).toBeInTheDocument();
  });
});
