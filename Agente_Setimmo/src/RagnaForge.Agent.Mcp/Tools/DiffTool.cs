using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class DiffTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_diff";
    public string Description => "Read an operation diff by operationId or last operation. Read-only.";
    public object InputSchema => SchemaFactory.Operation();

    public JsonOutput Execute(JsonElement arguments)
    {
        var operationId = GetString(arguments, "operationId");
        var last = GetBool(arguments, "last") ?? string.IsNullOrWhiteSpace(operationId);

        if (!last)
        {
            var invalid = McpToolPolicy.ValidateOperationId(operationId, "diff");
            if (invalid is not null) return invalid;
        }

        return McpResponseLimiter.Limit(new DiffCommand(context.AgentRoot, operationId, last).Execute());
    }

    private static string? GetString(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(name, out var prop) &&
        prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;

    private static bool? GetBool(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(name, out var prop) &&
        prop.ValueKind is JsonValueKind.True or JsonValueKind.False ? prop.GetBoolean() : null;
}
