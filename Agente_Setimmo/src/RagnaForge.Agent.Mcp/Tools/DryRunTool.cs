using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Security;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class DryRunTool(McpToolContext context, string entityType) : IMcpTool
{
    public string Name => $"ragnaforge_dry_run_{entityType}";
    public string Description => $"Plan a {entityType} change without applying it. Writes only inside agentRoot logs and inputs/dry-run.";
    public object InputSchema => SchemaFactory.DryRun();

    public JsonOutput Execute(JsonElement arguments)
    {
        var operationId = RagnaForge.Agent.Core.Output.JsonOutput.GenerateOperationId();
        var inputDir = Path.Combine(context.AgentRoot, "inputs", "dry-run");
        var inputPath = Path.Combine(inputDir, $"mcp-{operationId}.json");

        try
        {
            var loader = new ConfigLoader(context.ConfigDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var guard = new PathGuard(profile.WritableRoots, profile.ReadOnlyRoots, safetyConfig.BlockLubEditing);

            var normalizedInput = PathGuard.Normalize(inputPath);
            var normalizedAgentRoot = PathGuard.Normalize(context.AgentRoot);
            if (!PathGuard.IsContainedIn(normalizedInput, normalizedAgentRoot))
                return JsonOutput.Error("dry-run", "MCP dry-run input path must stay inside agentRoot.");

            var writeCheck = guard.EnsureCanWrite(inputPath);
            if (!writeCheck.IsAllowed)
                return JsonOutput.Error("dry-run", writeCheck.Reason ?? "MCP dry-run input write blocked.");

            Directory.CreateDirectory(inputDir);
            File.WriteAllText(inputPath, JsonSerializer.Serialize(arguments, new JsonSerializerOptions { WriteIndented = true }));

            return McpResponseLimiter.Limit(new DryRunCommand(
                context.ConfigDir, context.AgentRoot, entityType, inputPath).Execute());
        }
        catch (Exception ex)
        {
            return JsonOutput.Error("dry-run", ex.Message);
        }
    }
}
