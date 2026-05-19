using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ProfileListTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_profile_list";
    public string Description => "List configured profiles. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new ProfileCommand(context.ConfigDir, context.AgentRoot, "list").Execute());
}

public sealed class ProfileValidateTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_profile_validate";
    public string Description => "Validate active profile safety. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new ProfileCommand(context.ConfigDir, context.AgentRoot, "validate").Execute());
}
