using RagnaForge.Agent.Core.Entities;

namespace RagnaForge.Agent.Core.Commands;

public sealed class ValidationDecisionSummary
{
    public bool SafeForReadOnlyWork { get; set; }
    public bool SafeForDryRun { get; set; }
    public bool SafeForApply { get; set; }
    public Dictionary<string, int> IssueSummaryByScope { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> IssueSummaryByBlockingTarget { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class ValidationOperationalClassifier
{
    private static readonly string[] AllScopes = ["project-code", "external-data", "config", "cache", "security", "agent-runtime"];
    private static readonly string[] AllBlockingTargets = ["apply", "dry-run", "read-only-audit"];

    public static void ApplyClassification(IEnumerable<ValidationIssue> issues, string currentTask = "read-only-audit")
    {
        foreach (var issue in issues)
        {
            ClassifyIssue(issue, currentTask);
        }
    }

    public static ValidationDecisionSummary BuildSummary(IEnumerable<ValidationIssue> issues)
    {
        var issueList = issues.ToList();
        var summary = new ValidationDecisionSummary
        {
            SafeForReadOnlyWork = !issueList.Any(i => IsBlockedFor(i, "read-only-audit")),
            SafeForDryRun = !issueList.Any(i => IsBlockedFor(i, "dry-run")),
            SafeForApply = !issueList.Any(i => IsBlockedFor(i, "apply"))
        };

        foreach (var scope in AllScopes)
        {
            summary.IssueSummaryByScope[scope] = issueList.Count(i => string.Equals(i.Scope, scope, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var target in AllBlockingTargets)
        {
            summary.IssueSummaryByBlockingTarget[target] = issueList.Count(i => IsBlockedFor(i, target));
        }

        return summary;
    }

    public static ValidationIssue CreateCacheIssue(string code, string message, string recommendation, string severity = "error")
    {
        var issue = new ValidationIssue
        {
            Severity = severity,
            Scope = "cache",
            Code = code,
            Message = message,
            Recommendation = recommendation
        };

        issue.BlockingFor.AddRange(["apply", "dry-run", "read-only-audit"]);
        issue.SafeForCurrentTask = false;
        return issue;
    }

    public static ValidationIssue CreateSecurityIssue(string code, string message, string recommendation, string severity = "critical")
    {
        var issue = new ValidationIssue
        {
            Severity = severity,
            Scope = "security",
            Code = code,
            Message = message,
            Recommendation = recommendation
        };

        issue.BlockingFor.AddRange(["apply", "dry-run", "read-only-audit"]);
        issue.SafeForCurrentTask = false;
        return issue;
    }

    private static void ClassifyIssue(ValidationIssue issue, string currentTask)
    {
        issue.Scope = ClassifyScope(issue);
        issue.BlockingFor = ClassifyBlockingTargets(issue);
        issue.NotBlockingFor = AllBlockingTargets
            .Where(target => !issue.BlockingFor.Contains(target, StringComparer.OrdinalIgnoreCase))
            .ToList();
        issue.SafeForCurrentTask = !issue.BlockingFor.Contains(currentTask, StringComparer.OrdinalIgnoreCase);
        EnrichWithKnowledge(issue);
    }

    private static void EnrichWithKnowledge(ValidationIssue issue)
    {
        if (issue.Code == null) return;

        switch (issue.Code.ToUpperInvariant())
        {
            case "ITEM_DUPLICATE_ID_SERVER":
            case "ITEM_DUPLICATE_ID_CROSS_DB_MODE":
                issue.KnowledgeHints.Add("Check 'rathena.item.db_yaml' for database uniqueness rules.");
                issue.RecommendedKnowledgeEntryIds.Add("rathena.item.db_yaml");
                break;

            case "ITEM_MISSING_AEGIS":
                issue.KnowledgeHints.Add("Every server item must have a unique AegisName mapped to client asset lookups.");
                issue.RecommendedKnowledgeEntryIds.Add("rathena.item.db_yaml");
                break;

            case "MOB_DUPLICATE_ID":
                issue.KnowledgeHints.Add("Check 'rathena.mob.db_yaml' for monster database constraints.");
                issue.RecommendedKnowledgeEntryIds.Add("rathena.mob.db_yaml");
                break;

            case "NPC_NO_MAP":
            case "NPC_INVALID_COORDS":
                issue.KnowledgeHints.Add("Check 'rathena.npc.syntax' and 'rathena.npc.duplicate_names' for proper NPC mapping.");
                issue.RecommendedKnowledgeEntryIds.Add("rathena.npc.syntax");
                issue.RecommendedKnowledgeEntryIds.Add("rathena.npc.duplicate_names");
                break;

            case "MAP_NO_CLIENT_FILES":
            case "MAP_INCOMPLETE_CLIENT":
                issue.KnowledgeHints.Add("Check 'rathena.map.client_trio' to ensure RSW, GND, and GAT files are packed.");
                issue.RecommendedKnowledgeEntryIds.Add("rathena.map.client_trio");
                break;

            case "SECURITY_LUB_BLOCKED":
                issue.KnowledgeHints.Add("Check 'ragnaforge.gov.limits' regarding security policy boundaries on compiled Luas.");
                issue.RecommendedKnowledgeEntryIds.Add("ragnaforge.gov.limits");
                break;
        }
    }

    private static string ClassifyScope(ValidationIssue issue)
    {
        if (Matches(issue, "SECURITY", "PATH_", "GRF_", "LUB_"))
        {
            return "security";
        }

        if (Matches(issue, "CACHE_", "INDEX_", "PROFILE_", "FINGERPRINT_"))
        {
            return "cache";
        }

        if (Matches(issue, "CONFIG_", "PATHCONFIG_"))
        {
            return "config";
        }

        if (Matches(issue, "AGENT_"))
        {
            return "agent-runtime";
        }

        return "external-data";
    }

    private static List<string> ClassifyBlockingTargets(ValidationIssue issue)
    {
        var blockingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.Equals(issue.Scope, "security", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Severity, "critical", StringComparison.OrdinalIgnoreCase))
        {
            blockingTargets.UnionWith(AllBlockingTargets);
            return [.. blockingTargets];
        }

        if (string.Equals(issue.Scope, "config", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Severity, "critical", StringComparison.OrdinalIgnoreCase)))
        {
            blockingTargets.UnionWith(AllBlockingTargets);
            return [.. blockingTargets];
        }

        if (string.Equals(issue.Scope, "cache", StringComparison.OrdinalIgnoreCase))
        {
            blockingTargets.UnionWith(["apply", "dry-run", "read-only-audit"]);
            return [.. blockingTargets];
        }

        if (string.Equals(issue.Scope, "external-data", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Severity, "critical", StringComparison.OrdinalIgnoreCase))
            {
                blockingTargets.Add("apply");
            }

            return [.. blockingTargets];
        }

        if (string.Equals(issue.Severity, "critical", StringComparison.OrdinalIgnoreCase))
        {
            blockingTargets.UnionWith(["apply", "dry-run"]);
        }
        else if (string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
        {
            blockingTargets.Add("apply");
        }

        return [.. blockingTargets];
    }

    private static bool IsBlockedFor(ValidationIssue issue, string target) =>
        issue.BlockingFor.Contains(target, StringComparer.OrdinalIgnoreCase);

    private static bool Matches(ValidationIssue issue, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (issue.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
