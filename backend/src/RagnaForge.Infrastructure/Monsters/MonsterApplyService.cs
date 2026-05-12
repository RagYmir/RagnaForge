using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Monsters;
using RagnaForge.Infrastructure;

namespace RagnaForge.Infrastructure.Monsters;

public sealed partial class MonsterApplyService(string workspaceRoot)
{
    private const string SchemaVersion = "1.1";

    private static readonly string[] MobDatabaseFiles =
    [
        "db/pre-re/mob_db.yml",
        "db/re/mob_db.yml",
        "db/import/mob_db.yml"
    ];

    public MonsterApplyResult Apply(
        RepositoryPaths repositoryPaths,
        MonsterDryRunReport report)
    {
        ArgumentNullException.ThrowIfNull(repositoryPaths);
        ArgumentNullException.ThrowIfNull(report);

        if (!report.CanApply)
        {
            throw new InvalidOperationException("Monster dry-run is not applicable; apply was blocked.");
        }

        var operationId = BuildOperationId("monster-apply");
        var backupRoot = Path.Combine(workspaceRoot, "data", "backups", "monsters", operationId);
        var stagingRoot = Path.Combine(backupRoot, "staging");
        var applyLogPath = Path.Combine(workspaceRoot, "data", "logs", "monsters", $"{operationId}.apply.json");
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(applyLogPath)!);

        var allowedRoots = new[]
        {
            Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "db", "import"),
            Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "npc")
        };

        var files = new List<ItemAppliedFileSnapshot>();
        var conflicts = new List<ItemApplyConflict>();
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Monster apply requested for {report.ProposedChanges.Count} proposed change(s).")
        };
        var messages = new List<string>();
        PostWriteValidationSummary? postWriteValidation = null;

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
                conflicts.Add(new ItemApplyConflict(fullTargetPath, change.ChangeKind, "path.outside-monster-roots", ex.Message));
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

        conflicts.AddRange(AnalyzeMonsterConflicts(repositoryPaths, report));

        if (conflicts.Count > 0)
        {
            auditTrail.Add(BuildAudit("blocked", $"Monster apply blocked with {conflicts.Count} conflict(s)."));
            messages.Add($"Monster apply blocked before writing because {conflicts.Count} conflict(s) were detected.");
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

        var stagedPlans = new List<StagedWritePlan>();

        try
        {
            foreach (var change in report.ProposedChanges)
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
                File.WriteAllText(stagePath, newContent, Encoding.UTF8);
                auditTrail.Add(BuildAudit("stage", $"Staged monster file at {stagePath}.", fullTargetPath));

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
                messages.Add("Monster apply was blocked because staging validation failed.");
                messages.AddRange(validationMessages);
                auditTrail.Add(BuildAudit("blocked", "Post-write staging validation failed; final replacement was not attempted."));

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
                auditTrail.Add(BuildAudit("write", $"Applied {plan.Change.ChangeKind} monster change.", plan.TargetPath));

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
                    FirstMeaningfulLine(plan.Change.Preview));
                files.Add(snapshot);
                ValidateWrittenFile(snapshot);
                auditTrail.Add(BuildAudit("postvalidate", "Written monster target hash matches the apply manifest.", plan.TargetPath));
            }

            auditTrail.Add(BuildAudit("complete", $"Applied {files.Count} monster file change(s)."));
            messages.Add($"Applied {files.Count} monster file change(s) to rAthena targets.");
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
            messages.Add("Monster apply failed after partial work; automatic rollback was attempted.");
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

    public MonsterRollbackResult Rollback(string applyLogPath)
    {
        if (string.IsNullOrWhiteSpace(applyLogPath))
        {
            throw new InvalidOperationException("Monster apply log path is required for rollback.");
        }

        var fullApplyLogPath = Path.GetFullPath(applyLogPath);
        EnsurePathInsideWorkspace(fullApplyLogPath);

        var log = JsonSerializer.Deserialize<MonsterApplyLog>(File.ReadAllText(fullApplyLogPath))
                  ?? throw new InvalidOperationException("Monster apply log could not be deserialized.");
        if (!string.Equals(log.Status, "Applied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Rollback only accepts logs with status Applied; current status is {log.Status}.");
        }

        var rollbackOperationId = BuildOperationId("monster-rollback");
        var rollbackLogPath = Path.Combine(workspaceRoot, "data", "logs", "monsters", $"{rollbackOperationId}.rollback.json");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackLogPath)!);
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Monster rollback requested for apply log {Path.GetFileName(fullApplyLogPath)}.")
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
                auditTrail.Add(BuildAudit("delete", "Deleted monster file created by apply.", file.TargetPath));
            }
        }

        var messages = new List<string>
        {
            $"Rolled back {log.Files.Count} monster file change(s) from apply log {Path.GetFileName(fullApplyLogPath)}."
        };

        var rollbackLog = new MonsterRollbackResult(
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

    private static IReadOnlyList<ItemApplyConflict> AnalyzeMonsterConflicts(
        RepositoryPaths repositoryPaths,
        MonsterDryRunReport report)
    {
        var conflicts = new List<ItemApplyConflict>();
        foreach (var relativePath in MobDatabaseFiles)
        {
            var fullPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(fullPath))
            {
                var idMatch = MobIdRegex().Match(line);
                if (idMatch.Success
                    && int.TryParse(idMatch.Groups[1].Value, out var id)
                    && id == report.ResolvedId)
                {
                    conflicts.Add(new ItemApplyConflict(
                        fullPath,
                        "append",
                        "monster.id-present",
                        $"Monster ID {report.ResolvedId} already exists before apply.",
                        line.Trim()));
                }

                var aegisMatch = MobAegisRegex().Match(line);
                if (aegisMatch.Success
                    && aegisMatch.Groups[1].Value.Trim().Equals(report.Input.AegisName, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add(new ItemApplyConflict(
                        fullPath,
                        "append",
                        "monster.aegis-present",
                        $"Monster AegisName '{report.Input.AegisName}' already exists before apply.",
                        line.Trim()));
                }
            }
        }

        var mobAvailPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "db", "import", "mob_avail.yml");
        if (File.Exists(mobAvailPath)
            && !string.IsNullOrWhiteSpace(report.Input.SpriteOverride)
            && File.ReadAllText(mobAvailPath).Contains("Mob: " + report.Input.AegisName, StringComparison.OrdinalIgnoreCase))
        {
            conflicts.Add(new ItemApplyConflict(
                mobAvailPath,
                "append",
                "monster.mob-avail-present",
                $"mob_avail already contains an entry for '{report.Input.AegisName}'.",
                "Mob: " + report.Input.AegisName));
        }

        var mobSkillPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "db", "import", "mob_skill_db.txt");
        if (report.Skills.Count > 0 && File.Exists(mobSkillPath))
        {
            var lines = File.ReadLines(mobSkillPath).ToArray();
            foreach (var plan in report.Skills)
            {
                if (lines.Any(line => line.Contains(plan.Anchor, StringComparison.OrdinalIgnoreCase)))
                {
                    conflicts.Add(new ItemApplyConflict(
                        mobSkillPath,
                        "append",
                        "monster.skill-anchor-present",
                        $"mob_skill_db already contains the anchor '{plan.Anchor}'.",
                        plan.Anchor));
                }
            }
        }

        var spawnLoaderLine = report.ProposedChanges
            .FirstOrDefault(change => change.TargetPath.EndsWith("scripts_custom.conf", StringComparison.OrdinalIgnoreCase))
            ?.Preview;
        var scriptsCustomPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "npc", "scripts_custom.conf");
        if (!string.IsNullOrWhiteSpace(spawnLoaderLine)
            && File.Exists(scriptsCustomPath)
            && File.ReadAllText(scriptsCustomPath).Contains(spawnLoaderLine, StringComparison.OrdinalIgnoreCase))
        {
            conflicts.Add(new ItemApplyConflict(
                scriptsCustomPath,
                "append",
                "monster.spawn-loader-present",
                "scripts_custom.conf already contains the monster spawn loader line.",
                spawnLoaderLine));
        }

        var existingSpawnLines = ReadExistingSpawnLines(repositoryPaths.RathenaPath);
        foreach (var spawn in report.Spawns)
        {
            var line = BuildSpawnLine(report.ResolvedId, spawn);
            if (existingSpawnLines.Contains(line))
            {
                conflicts.Add(new ItemApplyConflict(
                    Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "npc", "custom"),
                    "create",
                    "monster.spawn-present",
                    $"npc/custom already contains the spawn line for '{spawn.Label ?? report.Input.DisplayName}'.",
                    line));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> ReadExistingSpawnLines(string rathenaPath)
    {
        var lines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npcCustomPath = Path.Combine(Path.GetFullPath(rathenaPath), "npc", "custom");
        if (!Directory.Exists(npcCustomPath))
        {
            return lines;
        }

        foreach (var file in Directory.EnumerateFiles(npcCustomPath, "*.txt", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("\tmonster\t", StringComparison.Ordinal))
                {
                    lines.Add(trimmed);
                }
            }
        }

        return lines;
    }

    private static string BuildSpawnLine(int resolvedId, MonsterSpawnPlan spawnPlan)
    {
        var builder = new StringBuilder();
        builder.Append(spawnPlan.MapName).Append(',');
        builder.Append(Math.Max(0, spawnPlan.X)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.Y)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.AreaX)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.AreaY)).Append('\t');
        builder.Append("monster\t");
        builder.Append(string.IsNullOrWhiteSpace(spawnPlan.Label) ? "Monster" : spawnPlan.Label).Append('\t');
        builder.Append(resolvedId).Append(',');
        builder.Append(Math.Max(1, spawnPlan.Amount)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.RespawnMilliseconds)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.RespawnMilliseconds));
        if (!string.IsNullOrWhiteSpace(spawnPlan.EventLabel))
        {
            builder.Append(',').Append(spawnPlan.EventLabel);
        }

        return builder.ToString();
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
                "Target monster file already exists; create operation would collide.",
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
                    "Target already contains the exact monster preview text.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(normalizedPreview)
                && normalizedPrevious.Contains(normalizedPreview, StringComparison.Ordinal))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.normalized-preview-present",
                    "Target already contains the proposed monster content after newline normalization.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(firstPreviewLine)
                && normalizedPrevious.Split('\n').Any(line => line.Trim().Equals(firstPreviewLine.Trim(), StringComparison.Ordinal)))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.anchor-line-present",
                    "Target already contains the first meaningful preview line; append may duplicate an existing monster block.",
                    firstPreviewLine));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private MonsterApplyLog BuildLog(
        string operationId,
        string applyLogPath,
        string backupRoot,
        RepositoryPaths repositoryPaths,
        MonsterDryRunReport report,
        string status,
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
            report.ResolvedId,
            status,
            files,
            conflicts,
            auditTrail,
            messages,
            postWriteValidation);

    private static MonsterApplyResult BuildResult(
        string operationId,
        bool applied,
        string applyLogPath,
        string backupRoot,
        MonsterDryRunReport report,
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
            throw new InvalidOperationException($"Monster target was not written: {file.TargetPath}");
        }

        var currentSha = ComputeSha256(File.ReadAllText(file.TargetPath));
        if (!currentSha.Equals(file.NewSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Monster target hash mismatch after write: {file.TargetPath}");
        }
    }

    private static void EnsureAllowedTarget(string fullTargetPath, IReadOnlyList<string> allowedRoots)
    {
        if (!allowedRoots.Any(root => IsPathInsideRoot(root, fullTargetPath)))
        {
            throw new InvalidOperationException($"Refusing to write outside monster rAthena roots: {fullTargetPath}");
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

    private static void WriteJsonFile<T>(string path, T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        File.WriteAllText(path, JsonSerializer.Serialize(value, options), Encoding.UTF8);
    }

    [GeneratedRegex(@"^\s*-\s+Id:\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex MobIdRegex();

    [GeneratedRegex(@"^\s*AegisName:\s*(\S+)", RegexOptions.Compiled)]
    private static partial Regex MobAegisRegex();

    private sealed record StagedWritePlan(
        ProposedFileChange Change,
        string TargetPath,
        string BackupPath,
        string StagingPath,
        bool ExistedBefore,
        string PreviousContent,
        string NewContent);
}
