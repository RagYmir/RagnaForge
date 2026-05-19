using System.Text.Json;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class McpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools;

    public McpToolRegistry(McpToolContext context)
    {
        IMcpTool[] tools =
        [
            new StatusTool(context),
            new DoctorTool(context),
            new BaselineTool(context),
            new HealthTool(context),
            new ScanTool(context),
            new ConfigGetTool(context),
            new ConfigValidateTool(context),
            new ProfileListTool(context),
            new ProfileValidateTool(context),
            new IndexTool(context),
            new FindTool(context, "item"),
            new FindTool(context, "npc"),
            new FindTool(context, "monster"),
            new FindTool(context, "map"),
            new ValidateTool(context),
            new DryRunTool(context, "item"),
            new DryRunTool(context, "npc"),
            new DryRunTool(context, "monster"),
            new DryRunTool(context, "map"),
            new DiffTool(context),
            new ReportTool(context),
            new ReportListTool(context),
            new ReportReadTool(context),
            new SecurityPolicyTool(context),
            new TriageTool(context),
            new RollbackListTool(context),
            new RollbackDryRunTool(context),
            new KnowledgeSourcesTool(context),
            new KnowledgeSearchTool(context),
            new KnowledgeExplainTool(context),
            new KnowledgeEntryTool(context),
            new KnowledgeSchemaTool(context),
            new KnowledgeValidateTool(context)
        ];

        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IMcpTool> Tools => _tools.Values;

    public JsonOutput Execute(string toolName, JsonElement arguments)
    {
        if (McpToolPolicy.IsBlocked(toolName))
            return toolName.Equals("ragnaforge_apply", StringComparison.OrdinalIgnoreCase)
                ? McpToolPolicy.BlockedApply()
                : McpToolPolicy.BlockedRollback();

        if (!McpToolPolicy.IsAllowed(toolName) || !_tools.TryGetValue(toolName, out var tool))
            return McpToolPolicy.UnknownTool(toolName);

        return tool.Execute(arguments);
    }
}
