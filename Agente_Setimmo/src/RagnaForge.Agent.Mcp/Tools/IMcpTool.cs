using System.Text.Json;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Mcp.Tools;

public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }
    JsonOutput Execute(JsonElement arguments);
}

public sealed record McpToolContext(string AgentRoot)
{
    public string ConfigDir => Path.Combine(AgentRoot, "config");
}
