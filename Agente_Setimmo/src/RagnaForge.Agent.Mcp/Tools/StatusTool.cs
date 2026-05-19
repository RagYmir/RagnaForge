using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class StatusTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_status";
    public string Description => "Return Agente Setimmo status. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new StatusCommand(context.ConfigDir).Execute());
}
