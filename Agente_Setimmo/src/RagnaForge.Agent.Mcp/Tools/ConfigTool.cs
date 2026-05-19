using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ConfigGetTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_config_get";
    public string Description => "Return active configuration. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new ConfigCommand(context.ConfigDir, context.AgentRoot, "get").Execute());
}

public sealed class ConfigValidateTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_config_validate";
    public string Description => "Validate active configuration safety. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new ConfigCommand(context.ConfigDir, context.AgentRoot, "validate").Execute());
}
