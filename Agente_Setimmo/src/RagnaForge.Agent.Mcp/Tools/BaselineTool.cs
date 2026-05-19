using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class BaselineTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_baseline";
    public string Description => "Run the operational baseline bundle (status, doctor, scan, index, validate). Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new BaselineCommand(context.ConfigDir, context.AgentRoot).Execute());
}
