using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Npcs;
using RagnaForge.Infrastructure;

namespace RagnaForge.Infrastructure.Npcs;

public sealed class NpcApplyService(string workspaceRoot)
{
    private const string SchemaVersion = "1.1";

    public NpcApplyResult Apply(
        RepositoryPaths repositoryPaths,
        NpcDryRunReport report,
        bool allowServerOnly = false)
    {
        ArgumentNullException.ThrowIfNull(repositoryPaths);
        ArgumentNullException.ThrowIfNull(report);

        var rathenaNpcRoot = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "npc");
        var patchDataRoot = Path.Combine(Path.GetFullPath(repositoryPaths.PatchPath), "data");
        var patchSystemRoot = Path.Combine(Path.GetFullPath(repositoryPaths.PatchPath), "system");
        var allowedRoots = new[]
        {
            rathenaNpcRoot,
            patchDataRoot,
            patchSystemRoot
        };

        var serverOnlyApplied = false;
        IReadOnlyList<ProposedFileChange> proposedChanges = report.ProposedChanges;

        if (!report.CanApply)
        {
            if (!allowServerOnly || !report.ServerCanApply)
            {
                throw new InvalidOperationException("NPC dry-run is not applicable; apply was blocked.");
            }

            serverOnlyApplied = true;
            proposedChanges = report.ProposedChanges
                .Where(change => IsPathInsideRoot(rathenaNpcRoot, change.TargetPath))
                .ToArray();
        }

        var operationId = BuildOperationId("npc-apply");
        var backupRoot = Path.Combine(workspaceRoot, "data", "backups", "npcs", operationId);
        var stagingRoot = Path.Combine(backupRoot, "staging");
        var applyLogPath = Path.Combine(workspaceRoot, "data", "logs", "npcs", $"{operationId}.apply.json");
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(applyLogPath)!);

        var files = new List<ItemAppliedFileSnapshot>();
        var conflicts = new List<ItemApplyConflict>();
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"NPC apply requested for {proposedChanges.Count} proposed change(s).")
        };
        var messages = new List<string>();
        PostWriteValidationSummary? postWriteValidation = null;

        if (serverOnlyApplied)
        {
            messages.Add("Client identity stayed blocked, so only the server-side NPC script/loader will be applied.");
            auditTrail.Add(BuildAudit("server-only", "Client identity stayed blocked; apply was reduced to server-side targets only."));
        }

        foreach (var change in proposedChanges)
        {
            var fullTargetPath = Path.GetFullPath(change.TargetPath);
            auditTrail.Add(BuildAudit("preflight", $"Validating target {fullTargetPath}.", fullTargetPath));

            try
            {
                EnsureAllowedTarget(fullTargetPath, allowedRoots);
            }
            catch (Exception ex)
            {
                conflicts.Add(new ItemApplyConflict(fullTargetPath, change.ChangeKind, "path.outside-npc-roots", ex.Message));
                continue;
            }

            var existedBefore = File.Exists(fullTargetPath);
            if (change.Exists != existedBefore)
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "target.existence-changed",
                    $"Target existence changed since dry-run. Dry-run Exists={change.Exists}, current Exists={existedBefore}."));
                continue;
            }

            var previousContent = existedBefore ? File.ReadAllText(fullTargetPath) : string.Empty;
            conflicts.AddRange(AnalyzeConflicts(change, fullTargetPath, existedBefore, previousContent));
        }

        if (conflicts.Count > 0)
        {
            auditTrail.Add(BuildAudit("blocked", $"NPC apply blocked with {conflicts.Count} conflict(s)."));
            messages.Add($"NPC apply blocked before writing because {conflicts.Count} conflict(s) were detected.");
            var blockedLog = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Blocked",
                serverOnlyApplied,
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, blockedLog);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, serverOnlyApplied, files, conflicts, auditTrail, messages, postWriteValidation);
        }

        var stagedPlans = new List<StagedWritePlan>();

        try
        {
            foreach (var change in proposedChanges)
            {
                var fullTargetPath = Path.GetFullPath(change.TargetPath);
                var existedBefore = File.Exists(fullTargetPath);
                var previousContent = existedBefore ? File.ReadAllText(fullTargetPath) : string.Empty;
                var backupPath = BuildBackupPath(backupRoot, fullTargetPath);
                var newContent = change.ChangeKind.Equals("create", StringComparison.OrdinalIgnoreCase)
                    ? change.Preview
                    : BuildUpdatedContent(previousContent, change.Preview);
                var stagePath = BuildBackupPath(stagingRoot, fullTargetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(stagePath)!);
                File.WriteAllText(stagePath, newContent, new UTF8Encoding(false));
                auditTrail.Add(BuildAudit("stage", $"Staged NPC file at {stagePath}.", fullTargetPath));

                stagedPlans.Add(new StagedWritePlan(
                    change,
                    fullTargetPath,
                    backupPath,
                    stagePath,
                    existedBefore,
                    previousContent,
                    newContent));
            }

            postWriteValidation = new ApplyPostWriteValidator().Validate(
                stagedPlans
                    .Select(plan => new StagedTextFile(plan.TargetPath, plan.StagingPath, plan.NewContent))
                    .ToArray());

            auditTrail.Add(BuildAudit(
                "postwrite-validation",
                $"Post-write staging validation completed with {postWriteValidation.Files.Count} file(s); valid={postWriteValidation.IsValid}."));

            if (!postWriteValidation.IsValid)
            {
                var validationMessages = postWriteValidation.Files
                    .SelectMany(file => file.Issues
                        .Where(issue => issue.Severity == PostWriteValidationSeverity.Error)
                        .Select(issue => $"{Path.GetFileName(file.TargetPath)}: {issue.Message}"))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                messages.Add("NPC apply was blocked because staging validation failed.");
                messages.AddRange(validationMessages);
                auditTrail.Add(BuildAudit("blocked", "Post-write staging validation failed; final replacement was not attempted."));

                var blockedLog = BuildLog(
                    operationId,
                    applyLogPath,
                    backupRoot,
                    repositoryPaths,
                    report,
                    "Blocked",
                    serverOnlyApplied,
                    files,
                    conflicts,
                    auditTrail,
                    messages,
                    postWriteValidation);
                WriteJsonFile(applyLogPath, blockedLog);

                return BuildResult(operationId, false, applyLogPath, backupRoot, report, serverOnlyApplied, files, conflicts, auditTrail, messages, postWriteValidation);
            }

            foreach (var plan in stagedPlans.Where(plan => plan.ExistedBefore))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plan.BackupPath)!);
                File.Copy(plan.TargetPath, plan.BackupPath, overwrite: true);
                auditTrail.Add(BuildAudit("backup", $"Backup created at {plan.BackupPath}.", plan.TargetPath));
            }

            foreach (var plan in stagedPlans)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plan.TargetPath)!);
                WriteFileAtomically(plan.TargetPath, plan.NewContent);
                auditTrail.Add(BuildAudit("write", $"Applied {plan.Change.ChangeKind} NPC change.", plan.TargetPath));

                var snapshot = new ItemAppliedFileSnapshot(
                    plan.TargetPath,
                    plan.BackupPath,
                    plan.ExistedBefore,
                    plan.Change.ChangeKind,
                    plan.PreviousContent.Length,
                    plan.NewContent.Length,
                    CountLines(plan.PreviousContent),
                    CountLines(plan.NewContent),
                    ComputeSha256OrNull(plan.PreviousContent, plan.ExistedBefore),
                    ComputeSha256(plan.NewContent),
                    plan.ExistedBefore && File.Exists(plan.BackupPath) ? ComputeSha256(File.ReadAllText(plan.BackupPath)) : null,
                    ComputeSha256(plan.Change.Preview),
                    FirstNonEmptyLine(plan.Change.Preview));
                files.Add(snapshot);
                ValidateWrittenFile(snapshot);
                auditTrail.Add(BuildAudit("postvalidate", "Written NPC target hash matches the apply manifest.", plan.TargetPath));
            }

            auditTrail.Add(BuildAudit("complete", $"Applied {files.Count} NPC file change(s)."));
            messages.Add(serverOnlyApplied
                ? $"Applied {files.Count} server-side NPC file change(s); client identity remained pending."
                : $"Applied {files.Count} NPC file change(s) to rAthena/Patch targets.");
            var log = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Applied",
                serverOnlyApplied,
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, log);

            return BuildResult(operationId, true, applyLogPath, backupRoot, report, serverOnlyApplied, files, conflicts, auditTrail, messages, postWriteValidation);
        }
        catch (Exception ex)
        {
            TryRollbackFiles(files);
            auditTrail.Add(BuildAudit("failed", ex.Message));
            messages.Add("NPC apply failed after partial work; automatic rollback was attempted.");
            messages.Add(ex.Message);
            var failedLog = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Failed",
                serverOnlyApplied,
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, failedLog);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, serverOnlyApplied, files, conflicts, auditTrail, messages, postWriteValidation);
        }
    }

    public NpcRollbackResult Rollback(string applyLogPath)
    {
        if (string.IsNullOrWhiteSpace(applyLogPath))
        {
            throw new InvalidOperationException("NPC apply log path is required for rollback.");
        }

        var fullApplyLogPath = Path.GetFullPath(applyLogPath);
        EnsurePathInsideWorkspace(fullApplyLogPath);

        var log = JsonSerializer.Deserialize<NpcApplyLog>(File.ReadAllText(fullApplyLogPath))
                  ?? throw new InvalidOperationException("NPC apply log could not be deserialized.");
        if (!string.Equals(log.Status, "Applied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Rollback only accepts logs with status Applied; current status is {log.Status}.");
        }

        var rollbackOperationId = BuildOperationId("npc-rollback");
        var rollbackLogPath = Path.Combine(workspaceRoot, "data", "logs", "npcs", $"{rollbackOperationId}.rollback.json");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackLogPath)!);
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"NPC rollback requested for apply log {Path.GetFileName(fullApplyLogPath)}.")
        };

        foreach (var file in log.Files)
        {
            if (!File.Exists(file.TargetPath))
            {
                if (file.ExistedBefore)
                {
                    throw new InvalidOperationException($"Target file is missing before rollback: {file.TargetPath}");
                }

                continue;
            }

            var currentSha = ComputeSha256(File.ReadAllText(file.TargetPath));
            if (!currentSha.Equals(file.NewSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Refusing rollback because target changed after apply: {file.TargetPath}");
            }
        }

        foreach (var file in log.Files.Reverse())
        {
            if (file.ExistedBefore)
            {
                if (!File.Exists(file.BackupPath))
                {
                    throw new InvalidOperationException($"Backup file is missing for rollback: {file.BackupPath}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);
                File.Copy(file.BackupPath, file.TargetPath, overwrite: true);
                auditTrail.Add(BuildAudit("restore", $"Restored backup {file.BackupPath}.", file.TargetPath));
            }
            else if (File.Exists(file.TargetPath))
            {
                File.Delete(file.TargetPath);
                auditTrail.Add(BuildAudit("delete", "Deleted NPC file created by apply.", file.TargetPath));
            }
        }

        var messages = new List<string>
        {
            $"Rolled back {log.Files.Count} NPC file change(s) from apply log {Path.GetFileName(fullApplyLogPath)}."
        };

        var rollbackLog = new NpcRollbackResult(
            DateTimeOffset.UtcNow,
            rollbackOperationId,
            true,
            Path.GetFullPath(rollbackLogPath),
            fullApplyLogPath,
            log.Files,
            auditTrail,
            messages);
        WriteJsonFile(rollbackLogPath, rollbackLog);
        return rollbackLog;
    }

    private void TryRollbackFiles(IReadOnlyList<ItemAppliedFileSnapshot> files)
    {
        foreach (var file in files.Reverse())
        {
            try
            {
                if (file.ExistedBefore)
                {
                    if (File.Exists(file.BackupPath))
                    {
                        File.Copy(file.BackupPath, file.TargetPath, overwrite: true);
                    }
                }
                else if (File.Exists(file.TargetPath))
                {
                    File.Delete(file.TargetPath);
                }
            }
            catch
            {
            }
        }
    }

    private static IReadOnlyList<ItemApplyConflict> AnalyzeConflicts(
        ProposedFileChange change,
        string fullTargetPath,
        bool existedBefore,
        string previousContent)
    {
        var conflicts = new List<ItemApplyConflict>();
        var normalizedPrevious = NormalizeNewLines(previousContent);
        var normalizedPreview = NormalizeNewLines(change.Preview);
        var firstPreviewLine = FirstNonEmptyLine(change.Preview);

        if (change.ChangeKind.Equals("create", StringComparison.OrdinalIgnoreCase) && existedBefore)
        {
            conflicts.Add(new ItemApplyConflict(
                fullTargetPath,
                change.ChangeKind,
                "create.target-exists",
                "Target NPC file already exists; create operation would collide.",
                firstPreviewLine));
        }

        if (change.ChangeKind.Equals("append", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(change.Preview)
                && previousContent.Contains(change.Preview, StringComparison.Ordinal))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.exact-preview-present",
                    "Target already contains the exact NPC preview text.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(normalizedPreview)
                && normalizedPrevious.Contains(normalizedPreview, StringComparison.Ordinal))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.normalized-preview-present",
                    "Target already contains the proposed NPC content after newline normalization.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(firstPreviewLine)
                && normalizedPrevious.Split('\n').Any(line => line.Trim().Equals(firstPreviewLine.Trim(), StringComparison.Ordinal)))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.anchor-line-present",
                    "Target already contains the first non-empty preview line; append may duplicate an existing NPC block.",
                    firstPreviewLine));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private NpcApplyLog BuildLog(
        string operationId,
        string applyLogPath,
        string backupRoot,
        RepositoryPaths repositoryPaths,
        NpcDryRunReport report,
        string status,
        bool serverOnlyApplied,
        IReadOnlyList<ItemAppliedFileSnapshot> files,
        IReadOnlyList<ItemApplyConflict> conflicts,
        IReadOnlyList<ItemApplyAuditEntry> auditTrail,
        IReadOnlyList<string> messages,
        PostWriteValidationSummary? postWriteValidation) =>
        new(
            SchemaVersion,
            operationId,
            DateTimeOffset.UtcNow,
            Path.GetFullPath(workspaceRoot),
            Path.GetFullPath(applyLogPath),
            Path.GetFullPath(backupRoot),
            repositoryPaths,
            report.Input,
            status,
            serverOnlyApplied,
            files,
            conflicts,
            auditTrail,
            messages,
            postWriteValidation);

    private static NpcApplyResult BuildResult(
        string operationId,
        bool applied,
        string applyLogPath,
        string backupRoot,
        NpcDryRunReport report,
        bool serverOnlyApplied,
        IReadOnlyList<ItemAppliedFileSnapshot> files,
        IReadOnlyList<ItemApplyConflict> conflicts,
        IReadOnlyList<ItemApplyAuditEntry> auditTrail,
        IReadOnlyList<string> messages,
        PostWriteValidationSummary? postWriteValidation) =>
        new(
            DateTimeOffset.UtcNow,
            operationId,
            applied,
            Path.GetFullPath(applyLogPath),
            Path.GetFullPath(backupRoot),
            report,
            serverOnlyApplied,
            files,
            conflicts,
            auditTrail,
            messages,
            postWriteValidation);

    private static string BuildUpdatedContent(string previousContent, string preview)
    {
        if (string.IsNullOrEmpty(previousContent))
        {
            return preview;
        }

        var builder = new StringBuilder(previousContent);
        if (!previousContent.EndsWith("\n", StringComparison.Ordinal))
        {
            builder.AppendLine();
        }

        builder.Append(preview);
        return builder.ToString();
    }

    private static void WriteFileAtomically(string targetPath, string content)
    {
        var tempPath = targetPath + ".ragnaforge.tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(false));

        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    private static void ValidateWrittenFile(ItemAppliedFileSnapshot snapshot)
    {
        var currentSha = ComputeSha256(File.ReadAllText(snapshot.TargetPath));
        if (!currentSha.Equals(snapshot.NewSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Written file hash mismatch for {snapshot.TargetPath}.");
        }
    }

    private static void EnsureAllowedTarget(string fullTargetPath, IReadOnlyList<string> allowedRoots)
    {
        if (!allowedRoots.Any(root => IsPathInsideRoot(root, fullTargetPath)))
        {
            throw new InvalidOperationException($"Refusing to write outside the allowed NPC roots: {fullTargetPath}");
        }
    }

    private void EnsurePathInsideWorkspace(string fullPath)
    {
        if (!IsPathInsideRoot(workspaceRoot, fullPath))
        {
            throw new InvalidOperationException($"Refusing to use a log outside the workspace: {fullPath}");
        }
    }

    private static bool IsPathInsideRoot(string root, string path)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.GetFullPath(path);
        var fullRootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(fullRootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBackupPath(string backupRoot, string fullTargetPath)
    {
        var drive = Path.GetPathRoot(fullTargetPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "root";
        var sanitizedDrive = drive.Replace(':', '_');
        var relative = fullTargetPath.Substring(Path.GetPathRoot(fullTargetPath)?.Length ?? 0)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(backupRoot, sanitizedDrive, relative);
    }

    private static string BuildOperationId(string prefix) =>
        $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{prefix}";

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FirstNonEmptyLine(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0)
        ?? string.Empty;

    private static int CountLines(string value) =>
        string.IsNullOrEmpty(value)
            ? 0
            : NormalizeNewLines(value).Split('\n').Length;

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string? ComputeSha256OrNull(string value, bool shouldCompute) =>
        shouldCompute ? ComputeSha256(value) : null;

    private static ItemApplyAuditEntry BuildAudit(string stage, string message, string? path = null) =>
        new(DateTimeOffset.UtcNow, stage, message, path);

    private static void WriteJsonFile<T>(string path, T payload)
    {
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            }),
            new UTF8Encoding(false));
    }

    private sealed record StagedWritePlan(
        ProposedFileChange Change,
        string TargetPath,
        string BackupPath,
        string StagingPath,
        bool ExistedBefore,
        string PreviousContent,
        string NewContent);
}
