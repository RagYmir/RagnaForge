using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class TriageTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_triage";
    public string Description => "Triage and analyze external validation issues. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["externalDataOnly"] = new { type = "boolean", description = "Only include external-data issues in analysis." }
        },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var externalOnly = true;
        if (arguments.ValueKind == JsonValueKind.Object &&
            arguments.TryGetProperty("externalDataOnly", out var prop) &&
            prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            externalOnly = prop.GetBoolean();
        }

        return McpResponseLimiter.Limit(new TriageCommand(context.ConfigDir, context.AgentRoot, externalOnly).Execute());
    }
}
