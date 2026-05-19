using System.Text.Json;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge diff --last/--operation'. Read-only diff viewer.
/// </summary>
public sealed class DiffCommand
{
    private readonly string _agentRoot;
    private readonly string? _operationId;
    private readonly bool _last;
    private static readonly JsonSerializerOptions JsonOpts = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DiffCommand(string agentRoot, string? operationId, bool last)
    {
        _agentRoot = agentRoot;
        _operationId = operationId;
        _last = last;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("diff");
        try
        {
            var opsDir = Path.Combine(_agentRoot, "logs", "operations");
            string? manifestPath = null;

            if (_last)
            {
                if (!Directory.Exists(opsDir))
                    return JsonOutput.Error("diff", "No operations found. Run 'ragnaforge dry-run' first.");
                manifestPath = Directory.GetFiles(opsDir, "*.json")
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            }
            else if (!string.IsNullOrWhiteSpace(_operationId))
            {
                if (!RagnaForge.Agent.Core.Security.OperationIdValidator.IsValid(_operationId))
                    return JsonOutput.Error("diff", "Invalid operationId format.");

                manifestPath = Path.Combine(opsDir, $"{_operationId}.json");
            }

            if (manifestPath is null || !File.Exists(manifestPath))
            {
                output = JsonOutput.Error("diff", _last
                    ? "No operations found."
                    : $"Operation '{_operationId}' not found.");
                output.NextRequiredAction = "run_dry_run";
                return output;
            }

            var manifest = JsonSerializer.Deserialize<OperationManifest>(
                File.ReadAllText(manifestPath), JsonOpts);
            if (manifest is null)
                return JsonOutput.Error("diff", "Could not read operation manifest.");

            // Load diff file if exists
            string? diffContent = null;
            if (!string.IsNullOrWhiteSpace(manifest.DiffPath))
            {
                var fullDiffPath = Path.Combine(_agentRoot, manifest.DiffPath);
                if (File.Exists(fullDiffPath)) diffContent = File.ReadAllText(fullDiffPath);
            }

            output.OperationId = manifest.OperationId;
            output.Summary = $"Diff for operation {manifest.OperationId} — {manifest.AffectedFiles.Count} file(s).";
            output.Data = new
            {
                manifest.OperationId, manifest.EntityType, manifest.Status, manifest.Applied,
                affectedFiles = manifest.AffectedFiles,
                diffPath = manifest.DiffPath,
                rollbackPlanPath = manifest.RollbackPlanPath
            };
        }
        catch (Exception ex) { output = JsonOutput.Error("diff", ex.Message); }
        return output;
    }
}

/// <summary>
/// Implements 'ragnaforge report'. Generates reports from operation manifests.
/// </summary>
public sealed class ReportCommand
{
    private readonly string _agentRoot;
    private readonly string? _operationId;
    private readonly bool _last;
    private readonly string _format; // json, md
    private static readonly JsonSerializerOptions JsonOpts = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ReportCommand(string agentRoot, string? operationId, bool last, string format = "json")
    {
        _agentRoot = agentRoot;
        _operationId = operationId;
        _last = last;
        _format = format;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("report");
        try
        {
            var opsDir = Path.Combine(_agentRoot, "logs", "operations");
            string? manifestPath = null;

            if (_last)
            {
                if (!Directory.Exists(opsDir))
                    return JsonOutput.Error("report", "No operations found.");
                manifestPath = Directory.GetFiles(opsDir, "*.json")
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            }
            else if (!string.IsNullOrWhiteSpace(_operationId))
            {
                if (!RagnaForge.Agent.Core.Security.OperationIdValidator.IsValid(_operationId))
                    return JsonOutput.Error("report", "Invalid operationId format.");

                manifestPath = Path.Combine(opsDir, $"{_operationId}.json");
            }

            if (manifestPath is null || !File.Exists(manifestPath))
                return JsonOutput.Error("report", "Operation not found.");

            var manifest = JsonSerializer.Deserialize<OperationManifest>(
                File.ReadAllText(manifestPath), JsonOpts);
            if (manifest is null)
                return JsonOutput.Error("report", "Could not read operation manifest.");

            // Save report
            var reportsDir = Path.Combine(_agentRoot, "logs", "reports");
            Directory.CreateDirectory(reportsDir);

            string reportPath;
            if (_format == "md")
            {
                reportPath = Path.Combine(reportsDir, $"{manifest.OperationId}.report.md");
                File.WriteAllText(reportPath, GenerateMarkdown(manifest));
            }
            else
            {
                reportPath = Path.Combine(reportsDir, $"{manifest.OperationId}.report.json");
                File.WriteAllText(reportPath, JsonSerializer.Serialize(manifest, JsonOpts));
            }

            output.OperationId = manifest.OperationId;
            output.Summary = $"Report generated for operation {manifest.OperationId}.";
            output.Data = new
            {
                manifest.OperationId, manifest.EntityType, manifest.Status, manifest.Applied,
                format = _format,
                reportPath = Path.GetRelativePath(_agentRoot, reportPath),
                affectedFiles = manifest.AffectedFiles.Count,
                validationIssues = manifest.ValidationIssues.Count,
                confirmationNotApplied = !manifest.Applied
            };
        }
        catch (Exception ex) { output = JsonOutput.Error("report", ex.Message); }
        return output;
    }

    private static string GenerateMarkdown(OperationManifest m)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Operation Report: {m.OperationId}");
        sb.AppendLine($"\n- **Type**: {m.EntityType} ({m.OperationType})");
        sb.AppendLine($"- **Status**: {m.Status}");
        sb.AppendLine($"- **Profile**: {m.ActiveProfile}");
        sb.AppendLine($"- **Fingerprint**: {m.ConfigFingerprint[..12]}...");
        sb.AppendLine($"- **Applied**: {m.Applied}");
        sb.AppendLine($"- **Created**: {m.CreatedAtUtc:u}");
        sb.AppendLine($"\n## Affected Files ({m.AffectedFiles.Count})");
        foreach (var f in m.AffectedFiles)
        {
            sb.AppendLine($"\n### {f.Action}: {f.Path}");
            sb.AppendLine($"{f.Description}");
            if (!string.IsNullOrWhiteSpace(f.DiffPreview))
                sb.AppendLine($"\n```diff\n{f.DiffPreview}\n```");
        }
        if (m.Errors.Count > 0)
        {
            sb.AppendLine($"\n## Errors ({m.Errors.Count})");
            foreach (var e in m.Errors) sb.AppendLine($"- ❌ {e}");
        }
        if (m.Warnings.Count > 0)
        {
            sb.AppendLine($"\n## Warnings ({m.Warnings.Count})");
            foreach (var w in m.Warnings) sb.AppendLine($"- ⚠️ {w}");
        }
        sb.AppendLine($"\n---\n*Generated by Agente Setimmo {RagnaForge.Agent.Core.AgentVersion.Current}. Nothing was applied.*");
        return sb.ToString();
    }
}

/// <summary>
/// Implements 'ragnaforge rollback'. Read-only rollback plan viewer.
/// </summary>
public sealed class RollbackCommand
{
    private readonly string _agentRoot;
    private readonly string? _rollbackId;
    private readonly bool _list;
    private readonly bool _dryRun;
    private readonly bool _confirm;
    private static readonly JsonSerializerOptions JsonOpts = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RollbackCommand(string agentRoot, string? rollbackId, bool list, bool dryRun, bool confirm)
    {
        _agentRoot = agentRoot;
        _rollbackId = rollbackId;
        _list = list;
        _dryRun = dryRun;
        _confirm = confirm;
    }

    public JsonOutput Execute()
    {
        // Real rollback is blocked
        if (_confirm)
        {
            return new JsonOutput
            {
                Ok = false, Mode = "rollback",
                Summary = "Real rollback is blocked in this MVP.",
                SafeForAutomation = false,
                NextRequiredAction = "blocked_by_safety_policy"
            };
        }

        var output = JsonOutput.Success("rollback");

        if (_list)
        {
            var rbDir = Path.Combine(_agentRoot, "logs", "rollbacks");
            var plans = new List<object>();
            if (Directory.Exists(rbDir))
            {
                foreach (var f in Directory.GetFiles(rbDir, "*.rollback.json").OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    plans.Add(new
                    {
                        id = Path.GetFileNameWithoutExtension(f).Replace(".rollback", ""),
                        path = Path.GetRelativePath(_agentRoot, f),
                        createdAtUtc = File.GetLastWriteTimeUtc(f)
                    });
                }
            }
            output.Summary = $"Found {plans.Count} rollback plan(s).";
            output.Data = new { plans };
            return output;
        }

        if (_dryRun && !string.IsNullOrWhiteSpace(_rollbackId))
        {
            if (!RagnaForge.Agent.Core.Security.OperationIdValidator.IsValid(_rollbackId))
                return JsonOutput.Error("rollback", "Invalid operationId format.");

            var rbPath = Path.Combine(_agentRoot, "logs", "rollbacks", $"{_rollbackId}.rollback.json");
            if (!File.Exists(rbPath))
                return JsonOutput.Error("rollback", $"Rollback plan '{_rollbackId}' not found.");

            var content = File.ReadAllText(rbPath);
            output.Summary = "Rollback is informational because real apply is not enabled.";
            output.Data = JsonSerializer.Deserialize<JsonElement>(content);
            return output;
        }

        return JsonOutput.Error("rollback", "Usage: ragnaforge rollback --list | --id <id> --dry-run");
    }
}
