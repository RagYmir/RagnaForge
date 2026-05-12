import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BytecodeBlockPanel } from "./BytecodeBlockPanel";

describe("BytecodeBlockPanel", () => {
  it("destaca bloqueio de bytecode", () => {
    const { container } = render(<BytecodeBlockPanel files={["itemInfo.lub"]} />);
    expect(screen.getByText(/Bytecode blocks/i)).toBeInTheDocument();
    expect(screen.getByText(/itemInfo\.lub/i)).toBeInTheDocument();
    expect(container.querySelector(".panel--danger")).toBeTruthy();
  });
});
