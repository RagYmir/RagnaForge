using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ScanTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_scan_project";
    public string Description => "Scan the configured project read-only and update cache inside agentRoot.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new ScanCommand(context.ConfigDir, context.AgentRoot).Execute());
}
