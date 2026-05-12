import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { DiffWorkbench } from "./DiffWorkbench";

describe("DiffWorkbench", () => {
  it("agrupa hunks por categoria", async () => {
    render(
      <DiffWorkbench
        title="Workbench"
        diffEntries={[
          {
            targetPath: "db/import/item_db.yml",
            changeKind: "Update",
            exists: true,
            preview: "+ item",
          },
          {
            targetPath: "data/luafiles514/lua files/datainfo/itemInfo.lua",
            changeKind: "Update",
            exists: true,
            preview: "+ client",
          },
        ]}
        tabs={[{ key: "json", label: "JSON", content: <div>ok</div> }]}
      />,
    );

    expect(screen.getByText("Server-side")).toBeInTheDocument();
    expect(screen.getByText("Client-side")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /Server-side/i }));
    expect(screen.getByText(/db\/import\/item_db\.yml/i)).toBeInTheDocument();
  });
});
