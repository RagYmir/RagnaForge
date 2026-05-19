using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Scanning;

namespace RagnaForge.Agent.Core.Commands;

public sealed class TriageCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly bool _externalOnly;

    public TriageCommand(string configDir, string agentRoot, bool externalOnly = true)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _externalOnly = externalOnly;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("triage");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            var cacheInspection = AgentCacheInspector.InspectEntityIndex(
                _agentRoot,
                pathsConfig.ActiveProfile,
                fingerprint);

            if (cacheInspection.Document is null)
            {
                output = JsonOutput.Error("triage", "Entity index not found or obsolete. Run 'ragnaforge index --entities --json' first.");
                output.NextRequiredAction = "run_index";
                return output;
            }

            var index = cacheInspection.Document;
            var issues = new List<ValidationIssue>();

            // Collect all validation issues
            issues.AddRange(ValidateCommand.ValidateItems(index));
            issues.AddRange(ValidateCommand.ValidateMonsters(index));
            issues.AddRange(ValidateCommand.ValidateNpcs(index));
            issues.AddRange(ValidateCommand.ValidateMaps(index));

            // Classify issues
            ValidationOperationalClassifier.ApplyClassification(issues);
            var decisionSummary = ValidationOperationalClassifier.BuildSummary(issues);

            if (_externalOnly)
            {
                issues = issues.Where(i => string.Equals(i.Scope, "external-data", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 1. Grouping
            var bySeverity = issues.GroupBy(i => i.Severity ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var byCode = issues.GroupBy(i => i.Code ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var byEntityType = issues.GroupBy(i => i.EntityType ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var byScope = issues.GroupBy(i => i.Scope ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var byFile = issues.GroupBy(i => string.IsNullOrWhiteSpace(i.SourceFile) ? "unknown" : Path.GetFileName(i.SourceFile))
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            // 2. Separation
            var errorsReal = issues.Where(i => string.Equals(i.Severity, "error", StringComparison.OrdinalIgnoreCase) || string.Equals(i.Severity, "critical", StringComparison.OrdinalIgnoreCase)).ToList();
            var warningsConhecidos = issues.Where(i => string.Equals(i.Severity, "warning", StringComparison.OrdinalIgnoreCase)).ToList();

            // Expected noise: warnings in external-data scope (e.g. MAP_NO_CLIENT_FILES)
            var ruidoEsperado = issues.Where(i => string.Equals(i.Scope, "external-data", StringComparison.OrdinalIgnoreCase) && string.Equals(i.Severity, "warning", StringComparison.OrdinalIgnoreCase)).ToList();

            var blockersApply = issues.Where(i => i.BlockingFor.Contains("apply", StringComparer.OrdinalIgnoreCase)).ToList();
            var naoBlockersReadOnly = issues.Where(i => i.NotBlockingFor.Contains("read-only-audit", StringComparer.OrdinalIgnoreCase)).ToList();
            var naoBlockersDryRun = issues.Where(i => i.NotBlockingFor.Contains("dry-run", StringComparer.OrdinalIgnoreCase)).ToList();

            // 3. Recommended actions
            var recommendedActions = new List<string>();
            if (ruidoEsperado.Count > 0)
            {
                recommendedActions.Add($"There are {ruidoEsperado.Count} external-data warnings (like MAP_NO_CLIENT_FILES). These are safe for read-only audit and dry-run work, but they block direct apply.");
                recommendedActions.Add("To resolve them, download the missing client files (.rsw/.gnd/.gat) or add these maps to an ignore baseline.");
            }
            if (errorsReal.Count > 0)
            {
                recommendedActions.Add($"Fix the {errorsReal.Count} real error(s) discovered during validation. These are critical blockers.");
            }
            if (recommendedActions.Count == 0)
            {
                recommendedActions.Add("No actions required. The system validation is clean.");
            }

            // 4. Summary
            var triageSummary = new
            {
                totalIssues = issues.Count,
                errors = errorsReal.Count,
                warnings = warningsConhecidos.Count,
                expectedNoise = ruidoEsperado.Count,
                applyBlockers = blockersApply.Count,
                safeForReadOnlyWork = decisionSummary.SafeForReadOnlyWork,
                safeForDryRun = decisionSummary.SafeForDryRun,
                safeForApply = decisionSummary.SafeForApply,
                grouping = new
                {
                    bySeverity,
                    byCode,
                    byEntityType,
                    byScope,
                    byFile
                },
                recommendedActions
            };

            // 5. Generate and save triage report
            var reportsDir = Path.Combine(_agentRoot, "logs", "reports");
            Directory.CreateDirectory(reportsDir);
            var reportPath = Path.Combine(reportsDir, "external-data-triage-v1.report.md");
            File.WriteAllText(reportPath, GenerateTriageMarkdown(triageSummary, errorsReal, ruidoEsperado, blockersApply));

            output.Summary = $"Triage completed - {issues.Count} external issues analyzed.";
            output.SafeForAutomation = decisionSummary.SafeForReadOnlyWork;
            output.Data = new
            {
                triageSummary,
                reportPath = Path.GetRelativePath(_agentRoot, reportPath)
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("triage", ex.Message);
        }

        return output;
    }

    private static string GenerateTriageMarkdown(object summaryObj, List<ValidationIssue> errors, List<ValidationIssue> noise, List<ValidationIssue> blockers)
    {
        dynamic s = summaryObj;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# External Data Triage Report (v1)");
        sb.AppendLine($"\nGenerated on: {DateTime.UtcNow:u}");
        sb.AppendLine($"\n## 1. Executive Summary");
        sb.AppendLine($"- **Total Issues Analyzed**: {s.totalIssues}");
        sb.AppendLine($"- **Real Errors**: {s.errors}");
        sb.AppendLine($"- **Warnings**: {s.warnings}");
        sb.AppendLine($"- **Expected Noise**: {s.expectedNoise}");
        sb.AppendLine($"- **Apply Blockers**: {s.applyBlockers}");
        sb.AppendLine($"\n### Safety Verdict");
        sb.AppendLine($"- **Safe for Read-Only Work**: {(s.safeForReadOnlyWork ? "YES" : "NO")}");
        sb.AppendLine($"- **Safe for Dry-Run Simulation**: {(s.safeForDryRun ? "YES" : "NO")}");
        sb.AppendLine($"- **Safe for Apply (Direct Write)**: {(s.safeForApply ? "YES" : "NO")}");

        sb.AppendLine($"\n## 2. Grouping Analysis");
        sb.AppendLine($"\n### By Severity");
        foreach (var kv in s.grouping.bySeverity) sb.AppendLine($"- **{kv.Key}**: {kv.Value}");

        sb.AppendLine($"\n### By Code");
        foreach (var kv in s.grouping.byCode) sb.AppendLine($"- **{kv.Key}**: {kv.Value}");

        sb.AppendLine($"\n### By Entity Type");
        foreach (var kv in s.grouping.byEntityType) sb.AppendLine($"- **{kv.Key}**: {kv.Value}");

        sb.AppendLine($"\n## 3. Recommended Actions");
        foreach (var action in s.recommendedActions) sb.AppendLine($"- {action}");

        if (errors.Count > 0)
        {
            sb.AppendLine($"\n## 4. Real Errors ({errors.Count})");
            foreach (var e in errors.Take(20))
            {
                sb.AppendLine($"- **{e.Code}** inside `{Path.GetFileName(e.SourceFile)}` (Line {e.Line}): {e.Message} *(Rec: {e.Recommendation})*");
            }
            if (errors.Count > 20) sb.AppendLine($"- *... and {errors.Count - 20} more errors.*");
        }

        if (blockers.Count > 0)
        {
            sb.AppendLine($"\n## 5. Apply Blockers ({blockers.Count})");
            foreach (var b in blockers.Take(20))
            {
                sb.AppendLine($"- **{b.Code}** ({b.EntityType} '{b.EntityName}'): {b.Message}");
            }
            if (blockers.Count > 20) sb.AppendLine($"- *... and {blockers.Count - 20} more blockers.*");
        }

        sb.AppendLine($"\n---\n*Report generated by Agente Setimmo Triage Command.*");
        return sb.ToString();
    }
}
