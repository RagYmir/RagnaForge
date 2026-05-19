using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ValidateTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_validate";
    public string Description => "Validate indexed entities. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["scope"] = new { type = "string", @enum = new[] { "all", "items", "npcs", "monsters", "maps", "client", "server" } }
        },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var scope = "all";
        if (arguments.ValueKind == JsonValueKind.Object &&
            arguments.TryGetProperty("scope", out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            scope = prop.GetString() ?? "all";
        }

        return McpResponseLimiter.Limit(new ValidateCommand(context.ConfigDir, context.AgentRoot, scope).Execute());
    }
}
