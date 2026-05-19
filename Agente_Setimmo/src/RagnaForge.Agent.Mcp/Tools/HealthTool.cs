using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class HealthTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_health";
    public string Description => "Return compact operational health for API, UI, and AI integrations. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new HealthCommand(context.ConfigDir, context.AgentRoot).Execute());
}
