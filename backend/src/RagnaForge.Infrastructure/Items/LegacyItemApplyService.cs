using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Infrastructure;

namespace RagnaForge.Infrastructure.Items;

public sealed class LegacyItemApplyService(string workspaceRoot)
{
    private const string SchemaVersion = "1.1";

    public ItemApplyResult Apply(
        RepositoryPaths repositoryPaths,
        ItemDryRunReport report)
    {
        ArgumentNullException.ThrowIfNull(repositoryPaths);
        ArgumentNullException.ThrowIfNull(report);

        if (!report.CanApply)
        {
            throw new InvalidOperationException("Dry-run is not applicable; apply was blocked.");
        }

        var operationId = BuildOperationId("item-apply");
        var backupRoot = Path.Combine(workspaceRoot, "data", "backups", "items", operationId);
        var applyLogPath = Path.Combine(workspaceRoot, "data", "logs", "items", $"{operationId}.apply.json");
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(applyLogPath)!);

        var allowedRoots = new[]
        {
            Path.GetFullPath(repositoryPaths.RathenaPath),
            Path.GetFullPath(repositoryPaths.PatchPath)
        };

        var files = new List<ItemAppliedFileSnapshot>();
        var conflicts = new List<ItemApplyConflict>();
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Apply requested for {report.ProposedChanges.Count} proposed change(s).")
        };
        var messages = new List<string>();

        foreach (var change in report.ProposedChanges)
        {
            var fullTargetPath = Path.GetFullPath(change.TargetPath);
            auditTrail.Add(BuildAudit("preflight", $"Validating target {fullTargetPath}.", fullTargetPath));

            try
            {
                EnsureAllowedTarget(fullTargetPath, allowedRoots);
            }
            catch (Exception ex)
            {
                conflicts.Add(new ItemApplyConflict(fullTargetPath, change.ChangeKind, "path.outside-roots", ex.Message));
                continue;
            }

            var existedBefore = File.Exists(fullTargetPath);
            var previousContent = existedBefore ? File.ReadAllText(fullTargetPath) : string.Empty;
            var targetConflicts = AnalyzeConflicts(change, fullTargetPath, existedBefore, previousContent);
            conflicts.AddRange(targetConflicts);
        }

        if (conflicts.Count > 0)
        {
            auditTrail.Add(BuildAudit("blocked", $"Apply blocked with {conflicts.Count} conflict(s)."));
            messages.Add($"Apply blocked before writing because {conflicts.Count} conflict(s) were detected.");
            var blockedLog = new ItemApplyLog(
                SchemaVersion,
                operationId,
                DateTimeOffset.UtcNow,
                Path.GetFullPath(workspaceRoot),
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                repositoryPaths,
                report.Input,
                report.ResolvedId,
                "Blocked",
                files,
                conflicts,
                auditTrail,
                messages);
            WriteJsonFile(applyLogPath, blockedLog);

            return new ItemApplyResult(
                DateTimeOffset.UtcNow,
                operationId,
                false,
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                report,
                files,
                conflicts,
                auditTrail,
                messages);
        }

        var stagedFiles = BuildStagedFiles(report.ProposedChanges, backupRoot);
        var postWriteValidation = new ApplyPostWriteValidator().Validate(stagedFiles);
        auditTrail.Add(BuildAudit("postwrite-validation", $"Validated {postWriteValidation.Files.Count} staged file(s); valid={postWriteValidation.IsValid}."));
        if (!postWriteValidation.IsValid)
        {
            conflicts.AddRange(postWriteValidation.Files
                .Where(file => !file.IsValid)
                .Select(file => new ItemApplyConflict(
                    file.TargetPath,
                    "validate",
                    "postwrite.invalid",
                    $"Post-write validation failed for staged file using {file.ValidatorName}.",
                    string.Join("; ", file.Issues.Where(issue => issue.Severity == PostWriteValidationSeverity.Error).Select(issue => issue.Message)))));
            auditTrail.Add(BuildAudit("blocked", $"Apply blocked by post-write validation with {conflicts.Count} conflict(s)."));
            messages.Add("Apply blocked before writing because staged client/server files failed validation.");
            var blockedLog = new ItemApplyLog(
                SchemaVersion,
                operationId,
                DateTimeOffset.UtcNow,
                Path.GetFullPath(workspaceRoot),
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                repositoryPaths,
                report.Input,
                report.ResolvedId,
                "Blocked",
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, blockedLog);

            return new ItemApplyResult(
                DateTimeOffset.UtcNow,
                operationId,
                false,
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                report,
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
        }

        try
        {
            foreach (var change in report.ProposedChanges)
            {
                var fullTargetPath = Path.GetFullPath(change.TargetPath);
                var existedBefore = File.Exists(fullTargetPath);
                var previousContent = existedBefore ? File.ReadAllText(fullTargetPath) : string.Empty;
                var backupPath = BuildBackupPath(backupRoot, fullTargetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

                if (existedBefore)
                {
                    File.Copy(fullTargetPath, backupPath, overwrite: true);
                    auditTrail.Add(BuildAudit("backup", $"Backup created at {backupPath}.", fullTargetPath));
                }

                var newContent = BuildUpdatedContent(previousContent, change.Preview);
                Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)!);
                WriteFileAtomically(fullTargetPath, newContent);
                auditTrail.Add(BuildAudit("write", $"Applied {change.ChangeKind} change.", fullTargetPath));

                files.Add(new ItemAppliedFileSnapshot(
                    fullTargetPath,
                    backupPath,
                    existedBefore,
                    change.ChangeKind,
                    previousContent.Length,
                    newContent.Length,
                    CountLines(previousContent),
                    CountLines(newContent),
                    ComputeSha256OrNull(previousContent, existedBefore),
                    ComputeSha256(newContent),
                    existedBefore && File.Exists(backupPath) ? ComputeSha256(File.ReadAllText(backupPath)) : null,
                    ComputeSha256(change.Preview),
                    FirstNonEmptyLine(change.Preview)));
            }

            auditTrail.Add(BuildAudit("complete", $"Applied {files.Count} file change(s)."));
            messages.Add($"Applied {files.Count} file change(s) to rAthena/Patch targets.");
            var log = new ItemApplyLog(
                SchemaVersion,
                operationId,
                DateTimeOffset.UtcNow,
                Path.GetFullPath(workspaceRoot),
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                repositoryPaths,
                report.Input,
                report.ResolvedId,
                "Applied",
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, log);

            return new ItemApplyResult(
                DateTimeOffset.UtcNow,
                operationId,
                true,
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                report,
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
        }
        catch (Exception ex)
        {
            TryRollbackFiles(files);
            auditTrail.Add(BuildAudit("failed", ex.Message));
            messages.Add("Apply failed after partial work; automatic rollback was attempted.");
            messages.Add(ex.Message);
            var failedLog = new ItemApplyLog(
                SchemaVersion,
                operationId,
                DateTimeOffset.UtcNow,
                Path.GetFullPath(workspaceRoot),
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                repositoryPaths,
                report.Input,
                report.ResolvedId,
                "Failed",
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, failedLog);

            return new ItemApplyResult(
                DateTimeOffset.UtcNow,
                operationId,
                false,
                Path.GetFullPath(applyLogPath),
                Path.GetFullPath(backupRoot),
                report,
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
        }
    }

    public ItemRollbackResult Rollback(string applyLogPath)
    {
        if (string.IsNullOrWhiteSpace(applyLogPath))
        {
            throw new InvalidOperationException("Apply log path is required for rollback.");
        }

        var fullApplyLogPath = Path.GetFullPath(applyLogPath);
        EnsurePathInsideWorkspace(fullApplyLogPath);

        var log = JsonSerializer.Deserialize<ItemApplyLog>(File.ReadAllText(fullApplyLogPath))
                  ?? throw new InvalidOperationException("Apply log could not be deserialized.");
        if (!string.Equals(log.Status, "Applied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Rollback only accepts logs with status Applied; current status is {log.Status}.");
        }

        var rollbackOperationId = BuildOperationId("item-rollback");
        var rollbackLogPath = Path.Combine(workspaceRoot, "data", "logs", "items", $"{rollbackOperationId}.rollback.json");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackLogPath)!);
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Rollback requested for apply log {Path.GetFileName(fullApplyLogPath)}.")
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
                auditTrail.Add(BuildAudit("delete", "Deleted file created by apply.", file.TargetPath));
            }
        }

        var messages = new List<string>
        {
            $"Rolled back {log.Files.Count} file change(s) from apply log {Path.GetFileName(fullApplyLogPath)}."
        };

        var rollbackLog = new ItemRollbackResult(
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

    private static IReadOnlyList<StagedTextFile> BuildStagedFiles(
        IReadOnlyList<ProposedFileChange> changes,
        string backupRoot)
    {
        var stagingRoot = Path.Combine(backupRoot, "_staging");
        var stagedFiles = new List<StagedTextFile>();
        foreach (var change in changes)
        {
            var fullTargetPath = Path.GetFullPath(change.TargetPath);
            var previousContent = File.Exists(fullTargetPath) ? File.ReadAllText(fullTargetPath) : string.Empty;
            var finalContent = change.ChangeKind.Equals("create", StringComparison.OrdinalIgnoreCase)
                ? change.Preview
                : BuildUpdatedContent(previousContent, change.Preview);
            var stagingPath = Path.Combine(stagingRoot, BuildStagingFileName(fullTargetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
            File.WriteAllText(stagingPath, finalContent, new UTF8Encoding(false));
            stagedFiles.Add(new StagedTextFile(fullTargetPath, stagingPath, finalContent));
        }

        return stagedFiles;
    }

    private static string BuildStagingFileName(string fullTargetPath)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(fullTargetPath.Select(character => invalid.Contains(character) || character is ':' or '\\' or '/' ? '_' : character).ToArray());
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
                "Target file already exists; create operation would collide.",
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
                    "Target already contains the exact preview text.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(normalizedPreview)
                && normalizedPrevious.Contains(normalizedPreview, StringComparison.Ordinal))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.normalized-preview-present",
                    "Target already contains the proposed content after newline normalization.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(firstPreviewLine)
                && NormalizeNewLines(previousContent).Split('\n').Any(line => line.Trim().Equals(firstPreviewLine.Trim(), StringComparison.Ordinal)))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.anchor-line-present",
                    "Target already contains the first non-empty preview line; append may duplicate an existing block.",
                    firstPreviewLine));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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
        File.WriteAllText(tempPath, content, Encoding.UTF8);

        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    private static void EnsureAllowedTarget(string fullTargetPath, IReadOnlyList<string> allowedRoots)
    {
        if (!allowedRoots.Any(root => fullTargetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Refusing to write outside rAthena/Patch roots: {fullTargetPath}");
        }
    }

    private void EnsurePathInsideWorkspace(string fullPath)
    {
        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        if (!fullPath.StartsWith(fullWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to use a log outside the workspace: {fullPath}");
        }
    }

    private static string BuildBackupPath(string backupRoot, string fullTargetPath)
    {
        var drive = Path.GetPathRoot(fullTargetPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "root";
        var driveToken = drive.Replace(':', '_').Replace("\\", string.Empty).Replace("/", string.Empty);
        var rootLength = Path.GetPathRoot(fullTargetPath)?.Length ?? 0;
        var relative = fullTargetPath[rootLength..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(backupRoot, driveToken, relative);
    }

    private static string BuildOperationId(string prefix) =>
        $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{prefix}-{Guid.NewGuid():N}";

    private static ItemApplyAuditEntry BuildAudit(string stage, string message, string? targetPath = null) =>
        new(DateTimeOffset.UtcNow, stage, message, targetPath);

    private static int CountLines(string content) =>
        string.IsNullOrEmpty(content)
            ? 0
            : NormalizeNewLines(content).Split('\n', StringSplitOptions.None).Length;

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string? FirstNonEmptyLine(string value) =>
        NormalizeNewLines(value)
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

    private static string? ComputeSha256OrNull(string content, bool shouldCompute) =>
        shouldCompute ? ComputeSha256(content) : null;

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static void WriteJsonFile<T>(string path, T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        File.WriteAllText(path, JsonSerializer.Serialize(value, options), Encoding.UTF8);
    }
}
