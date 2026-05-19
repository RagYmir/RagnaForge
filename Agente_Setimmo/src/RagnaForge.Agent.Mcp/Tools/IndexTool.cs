using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class IndexTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_index_entities";
    public string Description => "Index entities using safe enumeration. Writes cache only inside agentRoot.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new IndexCommand(context.ConfigDir, context.AgentRoot, "entities").Execute());
}
