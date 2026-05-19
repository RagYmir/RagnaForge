import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ValidationIssueTable } from "./ValidationIssueTable";

describe("ValidationIssueTable", () => {
  it("renders knowledge hints without destructive controls", () => {
    render(
      <ValidationIssueTable
        rows={[
          {
            key: "knowledge-hint",
            severity: "warning",
            category: "Knowledge",
            entity: "Item",
            file: "item_db.yml",
            origin: "validate",
            message: "Missing resource metadata.",
            recommendedAction: "Review knowledge hints.",
            blocksFutureApply: true,
            raw: {
              knowledgeHints: ["Check item resource naming rules."],
              recommendedKnowledgeEntryIds: ["item-db-resource-name"],
            },
          },
        ]}
      />,
    );

    expect(screen.getByText("Hint: Check item resource naming rules.")).toBeInTheDocument();
    expect(screen.getByText("Entry: item-db-resource-name")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /apply/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /rollback/i })).not.toBeInTheDocument();
  });
});
