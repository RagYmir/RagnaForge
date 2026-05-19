using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class RollbackListTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_rollback_list";
    public string Description => "List informational rollback plans. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new RollbackCommand(context.AgentRoot, null, list: true, dryRun: false, confirm: false).Execute());
}

public sealed class RollbackDryRunTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_rollback_dry_run";
    public string Description => "Preview an informational rollback plan. Real rollback remains blocked.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["rollbackId"] = new { type = "string" }
        },
        required = new[] { "rollbackId" },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var rollbackId = arguments.ValueKind == JsonValueKind.Object &&
                         arguments.TryGetProperty("rollbackId", out var prop) &&
                         prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

        var invalid = McpToolPolicy.ValidateOperationId(rollbackId, "rollback");
        if (invalid is not null) return invalid;

        return McpResponseLimiter.Limit(new RollbackCommand(
            context.AgentRoot, rollbackId, list: false, dryRun: true, confirm: false).Execute());
    }
}
