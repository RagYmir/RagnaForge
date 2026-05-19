using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class FindTool(McpToolContext context, string entityType) : IMcpTool
{
    public string Name => $"ragnaforge_find_{entityType}";
    public string Description => $"Find {entityType} entities from cached index. Does not auto-index.";
    public object InputSchema => SchemaFactory.Find(entityType is "item" or "monster");

    public JsonOutput Execute(JsonElement arguments)
    {
        var id = TryGetInt(arguments, "id");
        var name = TryGetString(arguments, "name");

        if (id is null && string.IsNullOrWhiteSpace(name))
            return JsonOutput.Error("find", entityType is "item" or "monster"
                ? "Provide either id or name."
                : "Provide name.");

        if (id is not null && entityType is not ("item" or "monster"))
            return JsonOutput.Error("find", $"{entityType} search only supports name in this preview.");

        return McpResponseLimiter.Limit(new FindCommand(
            context.ConfigDir, context.AgentRoot, entityType, id, name).Execute());
    }

    private static int? TryGetInt(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)) return value;
        return null;
    }

    private static string? TryGetString(JsonElement args, string name)
    {
        return args.ValueKind == JsonValueKind.Object &&
               args.TryGetProperty(name, out var prop) &&
               prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}
