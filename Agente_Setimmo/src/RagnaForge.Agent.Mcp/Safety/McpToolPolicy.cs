using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Mcp.Safety;

public static class McpToolPolicy
{
    public static readonly ISet<string> AllowedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ragnaforge_status",
        "ragnaforge_doctor",
        "ragnaforge_baseline",
        "ragnaforge_health",
        "ragnaforge_scan_project",
        "ragnaforge_config_get",
        "ragnaforge_config_validate",
        "ragnaforge_profile_list",
        "ragnaforge_profile_validate",
        "ragnaforge_index_entities",
        "ragnaforge_find_item",
        "ragnaforge_find_npc",
        "ragnaforge_find_monster",
        "ragnaforge_find_map",
        "ragnaforge_validate",
        "ragnaforge_dry_run_item",
        "ragnaforge_dry_run_npc",
        "ragnaforge_dry_run_monster",
        "ragnaforge_dry_run_map",
        "ragnaforge_diff",
        "ragnaforge_report",
        "ragnaforge_report_list",
        "ragnaforge_report_read",
        "ragnaforge_security_policy",
        "ragnaforge_triage",
        "ragnaforge_rollback_list",
        "ragnaforge_rollback_dry_run",
        "ragnaforge_knowledge_sources",
        "ragnaforge_knowledge_search",
        "ragnaforge_knowledge_explain",
        "ragnaforge_knowledge_entry",
        "ragnaforge_knowledge_schema",
        "ragnaforge_knowledge_validate"
    };

    public static readonly ISet<string> BlockedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ragnaforge_apply",
        "ragnaforge_rollback_confirm"
    };

    public static bool IsAllowed(string toolName) => AllowedTools.Contains(toolName);

    public static bool IsBlocked(string toolName) => BlockedTools.Contains(toolName);

    public static JsonOutput BlockedApply() => new()
    {
        Ok = false,
        Mode = "apply",
        Summary = "Real apply is blocked by safety policy.",
        SafeForAutomation = false,
        NextRequiredAction = "blocked_by_safety_policy"
    };

    public static JsonOutput BlockedRollback() => new()
    {
        Ok = false,
        Mode = "rollback",
        Summary = "Real rollback is blocked by safety policy.",
        SafeForAutomation = false,
        NextRequiredAction = "blocked_by_safety_policy"
    };

    public static JsonOutput UnknownTool(string toolName) => JsonOutput.Error("mcp", $"Unknown or disallowed MCP tool: {toolName}");

    public static JsonOutput? ValidateOperationId(string? operationId, string mode = "mcp")
    {
        return OperationIdValidator.IsValid(operationId)
            ? null
            : JsonOutput.Error(mode, "Invalid operationId format.");
    }
}
