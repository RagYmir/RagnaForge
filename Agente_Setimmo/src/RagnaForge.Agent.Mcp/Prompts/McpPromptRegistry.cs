namespace RagnaForge.Agent.Mcp.Prompts;

public sealed class McpPromptRegistry
{
    private static readonly IReadOnlyDictionary<string, PromptDefinition> Prompts =
        new Dictionary<string, PromptDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["ragnaforge_validate_project"] = new(
                "Validate the RagnaForge environment read-only.",
                "Run ragnaforge_status, ragnaforge_doctor, ragnaforge_validate, then summarize safeForReadOnlyWork, safeForDryRun, and safeForApply without applying anything."),
            ["ragnaforge_prepare_dry_run"] = new(
                "Prepare a safe dry-run payload.",
                "Inspect the entity request, use find/index tools first, then call only the matching dry-run tool. Do not apply, rollback, copy assets, or edit external files."),
            ["ragnaforge_review_validation_errors"] = new(
                "Review validation errors safely.",
                "Group validation issues by severity, scope, blockingFor, and source. Treat external-data warnings as read-only safe but apply-blocking until resolved."),
            ["ragnaforge_generate_report_summary"] = new(
                "Summarize a RagnaForge operation report.",
                "Use report list/read resources or tools, summarize warnings/errors/diff/readiness, and confirm no write action was performed."),
            ["ragnaforge_mcp_safety_briefing"] = new(
                "Explain MCP safety boundaries.",
                "Summarize that MCP exposes read-only, dry-run, diff, report, and informational rollback only. Real apply and real rollback remain blocked.")
        };

    public IReadOnlyList<object> ListPrompts() =>
        Prompts
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new
            {
                name = kvp.Key,
                description = kvp.Value.Description,
                arguments = Array.Empty<object>()
            })
            .ToArray();

    public object GetPrompt(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !Prompts.TryGetValue(name, out var prompt))
        {
            return new
            {
                description = "Unknown prompt.",
                messages = new[]
                {
                    Message("Unknown RagnaForge prompt. Use prompts/list to inspect safe prompt names.")
                }
            };
        }

        return new
        {
            description = prompt.Description,
            messages = new[]
            {
                Message(prompt.Text)
            }
        };
    }

    private static object Message(string text) => new
    {
        role = "user",
        content = new
        {
            type = "text",
            text
        }
    };

    private sealed record PromptDefinition(string Description, string Text);
}
