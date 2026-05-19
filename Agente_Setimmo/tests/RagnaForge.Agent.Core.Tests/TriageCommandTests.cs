using System.Text.Json;
using Xunit;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Core.Tests;

public sealed class TriageCommandTests
{
    [Fact]
    public void TriageCommand_WhenCacheMissing_ReturnsErrorOutput()
    {
        // Arrange
        var tempConfigDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var tempAgentRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempConfigDir);
        Directory.CreateDirectory(tempAgentRoot);
        Directory.CreateDirectory(Path.Combine(tempAgentRoot, "rAthena"));
        Directory.CreateDirectory(Path.Combine(tempAgentRoot, "patch"));

        try
        {
            // Write minimalist config files
            var pathsJson = JsonSerializer.Serialize(new
            {
                agentRoot = tempAgentRoot,
                activeProfile = "default",
                profiles = new Dictionary<string, object>
                {
                    ["default"] = new
                    {
                        dbMode = "renewal",
                        paths = new Dictionary<string, string>
                        {
                            ["rAthena"] = Path.Combine(tempAgentRoot, "rAthena"),
                            ["patch"] = Path.Combine(tempAgentRoot, "patch")
                        }
                    }
                }
            });
            File.WriteAllText(Path.Combine(tempConfigDir, "paths.json"), pathsJson);
            File.WriteAllText(Path.Combine(tempConfigDir, "safety.json"), "{}");

            var triage = new TriageCommand(tempConfigDir, tempAgentRoot);

            // Act
            var result = triage.Execute();

            // Assert
            Assert.False(result.Ok);
            Assert.Equal("run_index", result.NextRequiredAction);
        }
        finally
        {
            if (Directory.Exists(tempConfigDir)) Directory.Delete(tempConfigDir, true);
            if (Directory.Exists(tempAgentRoot)) Directory.Delete(tempAgentRoot, true);
        }
    }
}
