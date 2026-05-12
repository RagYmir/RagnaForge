import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ValidationMatrix } from "./ValidationMatrix";

describe("ValidationMatrix", () => {
  it("filtra por severidade", async () => {
    render(
      <ValidationMatrix
        warnings={["warning item"]}
        errors={["danger item"]}
        issues={[
          {
            key: "issue-warning",
            severity: "warning",
            category: "Client-side",
            entity: "Item",
            file: "itemInfo.lua",
            origin: "warning",
            message: "Warning message",
            recommendedAction: "Review",
          },
          {
            key: "issue-danger",
            severity: "danger",
            category: "Bytecode",
            entity: "NPC",
            file: "jobname.lub",
            origin: "blocked",
            message: "Danger message",
            recommendedAction: "Stop",
          },
        ]}
      />,
    );

    await userEvent.selectOptions(screen.getByLabelText("Severidade"), "danger");

    expect(screen.getByText(/Danger message/i)).toBeInTheDocument();
    expect(screen.queryByText(/Warning message/i)).not.toBeInTheDocument();
  });

  it("filtra por categoria", async () => {
    render(
      <ValidationMatrix
        issues={[
          {
            key: "issue-bytecode",
            severity: "danger",
            category: "Bytecode",
            entity: "NPC",
            file: "jobname.lub",
            origin: "blocked",
            message: "Bytecode message",
            recommendedAction: "Stop",
            blocksFutureApply: true,
          },
          {
            key: "issue-assets",
            severity: "warning",
            category: "Assets",
            entity: "Item",
            file: "inventory.bmp",
            origin: "missing",
            message: "Missing asset",
            recommendedAction: "Review",
          },
        ]}
      />,
    );

    await userEvent.selectOptions(screen.getAllByLabelText("Categoria")[1], "Bytecode");

    expect(screen.getByText(/Bytecode message/i)).toBeInTheDocument();
    expect(
      screen
        .queryAllByText(/Missing asset/i)
        .filter((element) => element.tagName !== "OPTION"),
    ).toHaveLength(0);
  });
});
