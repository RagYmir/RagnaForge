using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Infrastructure;

namespace RagnaForge.Infrastructure.Items;

public sealed partial class LegacyEquipmentApplyService(string workspaceRoot)
{
    private const string SchemaVersion = "1.0";

    private static readonly string[] ItemDatabaseFiles =
    [
        "db/pre-re/item_db.yml",
        "db/re/item_db.yml",
        "db/import/item_db.yml"
    ];

    public EquipmentApplyResult Apply(
        RepositoryPaths repositoryPaths,
        EquipmentDryRunReport report)
    {
        ArgumentNullException.ThrowIfNull(repositoryPaths);
        ArgumentNullException.ThrowIfNull(report);

        if (!report.CanApply)
        {
            throw new InvalidOperationException("Equipment dry-run is not applicable; apply was blocked.");
        }

        var operationId = BuildOperationId("equipment-apply");
        var backupRoot = Path.Combine(workspaceRoot, "data", "backups", "equipment", operationId);
        var applyLogPath = Path.Combine(workspaceRoot, "data", "logs", "equipment", $"{operationId}.apply.json");
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(applyLogPath)!);

        var allowedRoots = new[]
        {
            Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "db", "import"),
            Path.Combine(Path.GetFullPath(repositoryPaths.PatchPath), "data")
        };

        var files = new List<ItemAppliedFileSnapshot>();
        var conflicts = new List<ItemApplyConflict>();
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Equipment apply requested for {report.ProposedChanges.Count} proposed change(s).")
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
                conflicts.Add(new ItemApplyConflict(fullTargetPath, change.ChangeKind, "path.outside-equipment-roots", ex.Message));
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

        conflicts.AddRange(AnalyzeEquipmentConflicts(repositoryPaths, report));

        if (conflicts.Count > 0)
        {
            auditTrail.Add(BuildAudit("blocked", $"Equipment apply blocked with {conflicts.Count} conflict(s)."));
            messages.Add($"Equipment apply blocked before writing because {conflicts.Count} conflict(s) were detected.");
            var blockedLog = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Blocked",
                files,
                conflicts,
                auditTrail,
                messages);
            WriteJsonFile(applyLogPath, blockedLog);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages);
        }

        var stagedFiles = BuildStagedFiles(report.ProposedChanges, backupRoot);
        var postWriteValidation = new ApplyPostWriteValidator().Validate(stagedFiles);
        auditTrail.Add(BuildAudit("postwrite-validation", $"Validated {postWriteValidation.Files.Count} staged equipment file(s); valid={postWriteValidation.IsValid}."));
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
            auditTrail.Add(BuildAudit("blocked", $"Equipment apply blocked by post-write validation with {conflicts.Count} conflict(s)."));
            messages.Add("Equipment apply blocked before writing because staged files failed validation.");
            var blockedLog = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Blocked",
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, blockedLog);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages, postWriteValidation);
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

                var newContent = change.ChangeKind.Equals("create", StringComparison.OrdinalIgnoreCase)
                    ? change.Preview
                    : BuildUpdatedContent(previousContent, change.Preview);
                Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)!);
                WriteFileAtomically(fullTargetPath, newContent);
                auditTrail.Add(BuildAudit("write", $"Applied {change.ChangeKind} equipment change.", fullTargetPath));

                var snapshot = new ItemAppliedFileSnapshot(
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
                    FirstMeaningfulLine(change.Preview));
                files.Add(snapshot);
                ValidateWrittenFile(snapshot);
                auditTrail.Add(BuildAudit("postvalidate", "Written equipment target hash matches the apply manifest.", fullTargetPath));
            }

            auditTrail.Add(BuildAudit("complete", $"Applied {files.Count} equipment file change(s)."));
            messages.Add($"Applied {files.Count} equipment file change(s) to rAthena/Patch targets.");
            var log = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Applied",
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, log);

            return BuildResult(operationId, true, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages, postWriteValidation);
        }
        catch (Exception ex)
        {
            TryRollbackFiles(files);
            auditTrail.Add(BuildAudit("failed", ex.Message));
            messages.Add("Equipment apply failed after partial work; automatic rollback was attempted.");
            messages.Add(ex.Message);
            var failedLog = BuildLog(
                operationId,
                applyLogPath,
                backupRoot,
                repositoryPaths,
                report,
                "Failed",
                files,
                conflicts,
                auditTrail,
                messages,
                postWriteValidation);
            WriteJsonFile(applyLogPath, failedLog);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages, postWriteValidation);
        }
    }

    public EquipmentRollbackResult Rollback(string applyLogPath)
    {
        if (string.IsNullOrWhiteSpace(applyLogPath))
        {
            throw new InvalidOperationException("Equipment apply log path is required for rollback.");
        }

        var fullApplyLogPath = Path.GetFullPath(applyLogPath);
        EnsurePathInsideWorkspace(fullApplyLogPath);

        var log = JsonSerializer.Deserialize<EquipmentApplyLog>(File.ReadAllText(fullApplyLogPath))
                  ?? throw new InvalidOperationException("Equipment apply log could not be deserialized.");
        if (!string.Equals(log.Status, "Applied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Rollback only accepts logs with status Applied; current status is {log.Status}.");
        }

        var rollbackOperationId = BuildOperationId("equipment-rollback");
        var rollbackLogPath = Path.Combine(workspaceRoot, "data", "logs", "equipment", $"{rollbackOperationId}.rollback.json");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackLogPath)!);
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Equipment rollback requested for apply log {Path.GetFileName(fullApplyLogPath)}.")
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
                auditTrail.Add(BuildAudit("delete", "Deleted equipment file created by apply.", file.TargetPath));
            }
        }

        var messages = new List<string>
        {
            $"Rolled back {log.Files.Count} equipment file change(s) from apply log {Path.GetFileName(fullApplyLogPath)}."
        };

        var rollbackLog = new EquipmentRollbackResult(
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

    private static IReadOnlyList<ItemApplyConflict> AnalyzeEquipmentConflicts(
        RepositoryPaths repositoryPaths,
        EquipmentDryRunReport report)
    {
        var conflicts = new List<ItemApplyConflict>();

        foreach (var relativePath in ItemDatabaseFiles)
        {
            var fullPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(fullPath))
            {
                var idMatch = ItemIdRegex().Match(line);
                if (idMatch.Success
                    && int.TryParse(idMatch.Groups[1].Value, out var id)
                    && id == report.ResolvedId)
                {
                    conflicts.Add(new ItemApplyConflict(
                        fullPath,
                        "append",
                        "equipment.id-present",
                        $"Equipment item ID {report.ResolvedId} already exists before apply.",
                        line.Trim()));
                }

                var aegisMatch = ItemAegisRegex().Match(line);
                if (aegisMatch.Success
                    && aegisMatch.Groups[1].Value.Trim().Equals(report.Input.Item.AegisName, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add(new ItemApplyConflict(
                        fullPath,
                        "append",
                        "equipment.aegis-present",
                        $"Equipment AegisName '{report.Input.Item.AegisName}' already exists before apply.",
                        line.Trim()));
                }
            }
        }

        foreach (var change in report.ProposedChanges)
        {
            var fullPath = Path.GetFullPath(change.TargetPath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (fullPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                && LegacyTableIdRegex(report.ResolvedId).IsMatch(File.ReadAllText(fullPath)))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullPath,
                    change.ChangeKind,
                    "equipment.patch-id-present",
                    $"Legacy client table already contains ID {report.ResolvedId} before apply.",
                    $"{report.ResolvedId}#"));
            }

            var fileName = Path.GetFileName(fullPath);
            if (!KnownVisualDatainfoFiles.Contains(fileName))
            {
                continue;
            }

            var text = File.ReadAllText(fullPath);
            if (!string.IsNullOrWhiteSpace(report.Input.ClientSymbolName)
                && Regex.IsMatch(text, $@"\b{Regex.Escape(report.Input.ClientSymbolName)}\b", RegexOptions.CultureInvariant))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullPath,
                    change.ChangeKind,
                    "equipment.visual-symbol-present",
                    $"Visual symbol '{report.Input.ClientSymbolName}' already exists in datainfo.",
                    report.Input.ClientSymbolName));
            }

            if (report.Input.ViewId is > 0
                && TryGetVisualCategoryFromDatainfo(fileName) is { } category
                && IsVisualIdDeclared(text, category, report.Input.ViewId.Value))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullPath,
                    change.ChangeKind,
                    "equipment.view-id-present",
                    $"View ID {report.Input.ViewId} already exists in the visual ID table.",
                    report.Input.ViewId.Value.ToString()));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        var firstPreviewLine = FirstMeaningfulLine(change.Preview);

        if (change.ChangeKind.Equals("create", StringComparison.OrdinalIgnoreCase) && existedBefore)
        {
            conflicts.Add(new ItemApplyConflict(
                fullTargetPath,
                change.ChangeKind,
                "create.target-exists",
                "Target equipment file already exists; create operation would collide.",
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
                    "Target already contains the exact equipment preview text.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(normalizedPreview)
                && normalizedPrevious.Contains(normalizedPreview, StringComparison.Ordinal))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.normalized-preview-present",
                    "Target already contains the proposed equipment content after newline normalization.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(firstPreviewLine)
                && normalizedPrevious.Split('\n').Any(line => line.Trim().Equals(firstPreviewLine.Trim(), StringComparison.Ordinal)))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.anchor-line-present",
                    "Target already contains the first meaningful preview line; append may duplicate an existing equipment block.",
                    firstPreviewLine));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private EquipmentApplyLog BuildLog(
        string operationId,
        string applyLogPath,
        string backupRoot,
        RepositoryPaths repositoryPaths,
        EquipmentDryRunReport report,
        string status,
        IReadOnlyList<ItemAppliedFileSnapshot> files,
        IReadOnlyList<ItemApplyConflict> conflicts,
        IReadOnlyList<ItemApplyAuditEntry> auditTrail,
        IReadOnlyList<string> messages,
        PostWriteValidationSummary? postWriteValidation = null) =>
        new(
            SchemaVersion,
            operationId,
            DateTimeOffset.UtcNow,
            Path.GetFullPath(workspaceRoot),
            Path.GetFullPath(applyLogPath),
            Path.GetFullPath(backupRoot),
            repositoryPaths,
            report.Input,
            report.ResolvedId,
            status,
            files,
            conflicts,
            auditTrail,
            messages,
            postWriteValidation);

    private static EquipmentApplyResult BuildResult(
        string operationId,
        bool applied,
        string applyLogPath,
        string backupRoot,
        EquipmentDryRunReport report,
        IReadOnlyList<ItemAppliedFileSnapshot> files,
        IReadOnlyList<ItemApplyConflict> conflicts,
        IReadOnlyList<ItemApplyAuditEntry> auditTrail,
        IReadOnlyList<string> messages,
        PostWriteValidationSummary? postWriteValidation = null) =>
        new(
            DateTimeOffset.UtcNow,
            operationId,
            applied,
            Path.GetFullPath(applyLogPath),
            Path.GetFullPath(backupRoot),
            report,
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

    private static void ValidateWrittenFile(ItemAppliedFileSnapshot file)
    {
        if (!File.Exists(file.TargetPath))
        {
            throw new InvalidOperationException($"Equipment target was not written: {file.TargetPath}");
        }

        var currentSha = ComputeSha256(File.ReadAllText(file.TargetPath));
        if (!currentSha.Equals(file.NewSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Equipment target hash mismatch after write: {file.TargetPath}");
        }
    }

    private static void EnsureAllowedTarget(string fullTargetPath, IReadOnlyList<string> allowedRoots)
    {
        if (allowedRoots.Any(root => fullTargetPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                                     || fullTargetPath.Equals(root, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new InvalidOperationException($"Target path is outside the allowed equipment roots: {fullTargetPath}");
    }

    private void EnsurePathInsideWorkspace(string fullPath)
    {
        var normalizedWorkspace = Path.GetFullPath(workspaceRoot);
        if (!fullPath.StartsWith(normalizedWorkspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !fullPath.Equals(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path is outside the workspace: {fullPath}");
        }
    }

    private static string BuildBackupPath(string backupRoot, string fullTargetPath)
    {
        var drive = Path.GetPathRoot(fullTargetPath) ?? string.Empty;
        var relative = fullTargetPath.Substring(drive.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var sanitizedDrive = drive.Replace(':', '_').Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        return Path.Combine(backupRoot, sanitizedDrive, relative);
    }

    private static ItemApplyAuditEntry BuildAudit(string stage, string message, string? targetPath = null) =>
        new(DateTimeOffset.UtcNow, stage, message, targetPath);

    private static void WriteJsonFile<T>(string fullPath, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(fullPath, json, Encoding.UTF8);
    }

    private static int CountLines(string content) =>
        string.IsNullOrEmpty(content)
            ? 0
            : NormalizeNewLines(content).Split('\n', StringSplitOptions.None).Length;

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string? FirstMeaningfulLine(string value) =>
        NormalizeNewLines(value)
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));

    private static string? ComputeSha256OrNull(string content, bool shouldCompute) =>
        shouldCompute ? ComputeSha256(content) : null;

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static readonly HashSet<string> KnownVisualDatainfoFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "accessoryid.lub",
        "accname.lub",
        "spriterobeid.lub",
        "spriterobename.lub",
        "weapontable.lub"
    };

    private static string? TryGetVisualCategoryFromDatainfo(string fileName) =>
        fileName.ToLowerInvariant() switch
        {
            "accessoryid.lub" or "accname.lub" => "headgear",
            "spriterobeid.lub" or "spriterobename.lub" => "robe",
            "weapontable.lub" => "weapon",
            _ => null
        };

    private static bool IsVisualIdDeclared(string text, string category, int viewId)
    {
        var pattern = category switch
        {
            "headgear" => $@"ACCESSORY_IDs\s*=\s*\{{[\s\S]*?=\s*{viewId}\s*(,|\r|\n|\}})",
            "robe" => $@"SPRITE_ROBE_IDs\s*=\s*\{{[\s\S]*?=\s*{viewId}\s*(,|\r|\n|\}})",
            "weapon" => $@"Weapon_IDs\s*=\s*\{{[\s\S]*?=\s*{viewId}\s*(,|\r|\n|\}})",
            _ => ""
        };

        return pattern.Length > 0 && Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"^\s*-\s*Id:\s*(\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ItemIdRegex();

    [GeneratedRegex(@"^\s*AegisName:\s*([A-Za-z0-9_]+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ItemAegisRegex();

    private static Regex LegacyTableIdRegex(int id) =>
        new($@"(?m)^\s*{id}\#", RegexOptions.CultureInvariant);

    private static string BuildOperationId(string prefix)
    {
        var value = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        return value[..Math.Min(value.Length, 80)];
    }
}
