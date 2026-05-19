using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ReportTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_report";
    public string Description => "Generate an operation report inside agentRoot logs/reports.";
    public object InputSchema => SchemaFactory.Operation();

    public JsonOutput Execute(JsonElement arguments)
    {
        var operationId = GetString(arguments, "operationId");
        var last = GetBool(arguments, "last") ?? string.IsNullOrWhiteSpace(operationId);
        var format = GetString(arguments, "format") ?? "json";

        if (!last)
        {
            var invalid = McpToolPolicy.ValidateOperationId(operationId, "report");
            if (invalid is not null) return invalid;
        }

        return McpResponseLimiter.Limit(new ReportCommand(context.AgentRoot, operationId, last, format).Execute());
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
