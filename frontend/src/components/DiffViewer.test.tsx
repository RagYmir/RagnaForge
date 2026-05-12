import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DiffViewer } from "./DiffViewer";

describe("DiffViewer", () => {
  it("renderiza hunks de diff de forma legivel", () => {
    render(
      <DiffViewer
        diff={{
          fileCount: 1,
          entries: [
            {
              targetPath: "db/import/item_db.yml",
              changeKind: "Update",
              exists: true,
              unifiedDiff: "@@ item @@\n line\n+added\n-removed",
            },
          ],
        }}
      />,
    );

    expect(screen.getByText("db/import/item_db.yml")).toBeInTheDocument();
    expect(screen.getByText("+added")).toBeInTheDocument();
    expect(screen.getByText("-removed")).toBeInTheDocument();
  });
});
