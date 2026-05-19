import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SafetyBanner } from "./SafetyBanner";

describe("SafetyBanner", () => {
  it("mostra modo seguro, ApplyEnabled false e RollbackEnabled false", () => {
    render(
      <SafetyBanner
        status={{
          readOnlyMode: true,
          applyEndpointsEnabled: false,
          rollbackEndpointsEnabled: false,
          requireApiKey: true,
          apiKeyHeaderName: "X-RagnaForge-Api-Key",
          service: "RagnaForge API",
          mode: "read-only-dry-run-diff-preview",
          workspaceRoot: "C:/Users/Allis/Desktop/Ragna_Forge",
          maxRequestBodyBytes: 1048576,
          maxGrfContainersPerRequest: 50,
          maxDiffHunksPerResponse: 500,
          generatedAtUtc: new Date().toISOString(),
          disabledWriteOperations: ["Apply", "Rollback", "FileWrite"],
          capabilities: [],
        }}
      />,
    );

    expect(screen.getByText(/ReadOnlyMode = true/i)).toBeInTheDocument();
    expect(screen.getByText(/ApplyEnabled = false/i)).toBeInTheDocument();
    expect(screen.getByText(/RollbackEnabled = false/i)).toBeInTheDocument();
    expect(
      screen.getByText(
        /Apply e rollback nao existem nesta interface nesta fase\./i,
      ),
    ).toBeInTheDocument();
  });
});
