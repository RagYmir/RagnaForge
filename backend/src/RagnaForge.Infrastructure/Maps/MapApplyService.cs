using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Maps;
using RagnaForge.Infrastructure.GrfEditorIntegration;

namespace RagnaForge.Infrastructure.Maps;

public sealed class MapApplyService(
    string workspaceRoot,
    IGrfFileExtractor? grfFileExtractor = null,
    IMapCacheBuilder? mapCacheBuilder = null)
{
    private const string SchemaVersion = "1.0";
    private const long MaxExtractionBytes = 512L * 1024L * 1024L;

    private readonly IGrfFileExtractor _grfFileExtractor = grfFileExtractor ?? new GrfAssemblyFileExtractor();
    private readonly IMapCacheBuilder _mapCacheBuilder = mapCacheBuilder ?? new RathenaMapCacheBuilder(workspaceRoot);

    public MapApplyResult Apply(
        RepositoryPaths repositoryPaths,
        MapDryRunReport report)
    {
        ArgumentNullException.ThrowIfNull(repositoryPaths);
        ArgumentNullException.ThrowIfNull(report);

        if (!report.CanApply)
        {
            throw new InvalidOperationException("Map dry-run is not applicable; apply was blocked.");
        }

        var operationId = BuildOperationId("map-apply");
        var backupRoot = Path.Combine(workspaceRoot, "data", "backups", "maps", operationId);
        var applyLogPath = Path.Combine(workspaceRoot, "data", "logs", "maps", $"{operationId}.apply.json");
        var operationTempRoot = Path.Combine(workspaceRoot, "tmp", "map-apply", operationId);
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(applyLogPath)!);
        Directory.CreateDirectory(operationTempRoot);

        var allowedRoots = new[]
        {
            Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "db", "import"),
            Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "conf"),
            Path.Combine(Path.GetFullPath(repositoryPaths.PatchPath), "data")
        };

        var files = new List<MapAppliedFileSnapshot>();
        var conflicts = new List<ItemApplyConflict>();
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Map apply requested for {report.ProposedChanges.Count} text change(s) and {report.AssetPlans.Count(plan => plan.NeedsCopy)} asset copy action(s).")
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
                conflicts.Add(new ItemApplyConflict(fullTargetPath, change.ChangeKind, "path.outside-map-roots", ex.Message));
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
            conflicts.AddRange(AnalyzeTextConflicts(change, fullTargetPath, existedBefore, previousContent));
        }

        foreach (var plan in report.AssetPlans.Where(plan => plan.Required))
        {
            var fullTargetPath = Path.GetFullPath(plan.TargetPath);
            auditTrail.Add(BuildAudit("preflight", $"Validating map asset target {fullTargetPath}.", fullTargetPath));

            try
            {
                EnsureAllowedTarget(fullTargetPath, allowedRoots);
            }
            catch (Exception ex)
            {
                conflicts.Add(new ItemApplyConflict(fullTargetPath, "copy", "path.outside-map-roots", ex.Message));
                continue;
            }

            var existedBefore = File.Exists(fullTargetPath);
            if (plan.ExistsInTarget != existedBefore)
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    "copy",
                    "target.existence-changed",
                    $"Map asset existence changed since dry-run. Dry-run ExistsInTarget={plan.ExistsInTarget}, current Exists={existedBefore}.",
                    plan.RelativePath));
            }

            if (string.Equals(plan.SourceKind, "Missing", StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    "copy",
                    "map.asset-missing",
                    $"Required map asset '{plan.RelativePath}' is still unresolved.",
                    plan.RelativePath));
                continue;
            }

            if (plan.NeedsCopy && !CanResolveAssetSource(plan))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    "copy",
                    "map.asset-source-missing",
                    $"Required source for map asset '{plan.RelativePath}' is unavailable.",
                    plan.SourcePath));
            }
        }

        var mapCachePlan = report.MapCachePlan;
        if (mapCachePlan is null || !mapCachePlan.ToolDetected)
        {
            conflicts.Add(new ItemApplyConflict(
                Path.GetFullPath(mapCachePlan?.CachePath ?? Path.Combine(repositoryPaths.RathenaPath, "db", "import", "map_cache.dat")),
                "rebuild",
                "mapcache.tool-missing",
                "mapcache.exe is required for map apply."));
        }
        else
        {
            var fullMapCachePath = Path.GetFullPath(mapCachePlan.CachePath);
            try
            {
                EnsureAllowedTarget(fullMapCachePath, allowedRoots);
            }
            catch (Exception ex)
            {
                conflicts.Add(new ItemApplyConflict(fullMapCachePath, "rebuild", "path.outside-map-roots", ex.Message));
            }
        }

        conflicts.AddRange(AnalyzeMapRegistrationConflicts(repositoryPaths, report));

        if (conflicts.Count > 0)
        {
            auditTrail.Add(BuildAudit("blocked", $"Map apply blocked with {conflicts.Count} conflict(s)."));
            messages.Add($"Map apply blocked before writing because {conflicts.Count} conflict(s) were detected.");
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
            TryDeleteDirectory(operationTempRoot);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages);
        }

        try
        {
            ApplyTextChanges(report, backupRoot, files, auditTrail);
            ApplyAssetCopies(repositoryPaths, report, backupRoot, operationTempRoot, files, auditTrail, messages);
            ApplyMapCache(repositoryPaths, report, backupRoot, operationTempRoot, files, auditTrail, messages);

            auditTrail.Add(BuildAudit("complete", $"Applied {files.Count} map change(s)."));
            messages.Add($"Applied {files.Count} map change(s) to rAthena/Patch targets.");
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
                messages);
            WriteJsonFile(applyLogPath, log);
            TryDeleteDirectory(operationTempRoot);

            return BuildResult(operationId, true, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages);
        }
        catch (Exception ex)
        {
            TryRollbackFiles(files);
            auditTrail.Add(BuildAudit("failed", ex.Message));
            messages.Add("Map apply failed after partial work; automatic rollback was attempted.");
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
                messages);
            WriteJsonFile(applyLogPath, failedLog);
            TryDeleteDirectory(operationTempRoot);

            return BuildResult(operationId, false, applyLogPath, backupRoot, report, files, conflicts, auditTrail, messages);
        }
    }

    public MapRollbackResult Rollback(string applyLogPath)
    {
        if (string.IsNullOrWhiteSpace(applyLogPath))
        {
            throw new InvalidOperationException("Map apply log path is required for rollback.");
        }

        var fullApplyLogPath = Path.GetFullPath(applyLogPath);
        EnsurePathInsideWorkspace(fullApplyLogPath);

        var log = JsonSerializer.Deserialize<MapApplyLog>(File.ReadAllText(fullApplyLogPath))
                  ?? throw new InvalidOperationException("Map apply log could not be deserialized.");
        if (!string.Equals(log.Status, "Applied", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Rollback only accepts logs with status Applied; current status is {log.Status}.");
        }

        var rollbackOperationId = BuildOperationId("map-rollback");
        var rollbackLogPath = Path.Combine(workspaceRoot, "data", "logs", "maps", $"{rollbackOperationId}.rollback.json");
        Directory.CreateDirectory(Path.GetDirectoryName(rollbackLogPath)!);
        var auditTrail = new List<ItemApplyAuditEntry>
        {
            BuildAudit("start", $"Map rollback requested for apply log {Path.GetFileName(fullApplyLogPath)}.")
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

            var currentSha = ComputeSha256(File.ReadAllBytes(file.TargetPath));
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
                auditTrail.Add(BuildAudit("delete", "Deleted map file created by apply.", file.TargetPath));
            }
        }

        var messages = new List<string>
        {
            $"Rolled back {log.Files.Count} map change(s) from apply log {Path.GetFileName(fullApplyLogPath)}."
        };

        var rollbackLog = new MapRollbackResult(
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

    private void ApplyTextChanges(
        MapDryRunReport report,
        string backupRoot,
        IList<MapAppliedFileSnapshot> files,
        IList<ItemApplyAuditEntry> auditTrail)
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
            WriteTextAtomically(fullTargetPath, newContent);
            auditTrail.Add(BuildAudit("write", $"Applied {change.ChangeKind} map text change.", fullTargetPath));

            var previousBytes = existedBefore ? Encoding.UTF8.GetBytes(previousContent) : [];
            var newBytes = Encoding.UTF8.GetBytes(newContent);
            var snapshot = new MapAppliedFileSnapshot(
                fullTargetPath,
                backupPath,
                existedBefore,
                change.ChangeKind,
                false,
                previousBytes.LongLength,
                newBytes.LongLength,
                existedBefore ? ComputeSha256(previousBytes) : null,
                ComputeSha256(newBytes),
                existedBefore && File.Exists(backupPath) ? ComputeSha256(File.ReadAllBytes(backupPath)) : null,
                FirstMeaningfulLine(change.Preview));
            files.Add(snapshot);
            ValidateWrittenFile(snapshot);
            auditTrail.Add(BuildAudit("postvalidate", "Written map text target hash matches the apply manifest.", fullTargetPath));
        }
    }

    private void ApplyAssetCopies(
        RepositoryPaths repositoryPaths,
        MapDryRunReport report,
        string backupRoot,
        string operationTempRoot,
        IList<MapAppliedFileSnapshot> files,
        IList<ItemApplyAuditEntry> auditTrail,
        IList<string> messages)
    {
        var extractionRoot = Path.Combine(operationTempRoot, "grf-extract");
        var grfPlans = report.AssetPlans
            .Where(plan => plan.Required
                           && plan.NeedsCopy
                           && string.Equals(plan.SourceKind, "GrfExtraction", StringComparison.OrdinalIgnoreCase)
                           && plan.GrfMatch is not null)
            .ToArray();
        var extractedByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (grfPlans.Length > 0)
        {
            var extraction = _grfFileExtractor.ExtractFiles(
                repositoryPaths,
                grfPlans.Select(plan => plan.GrfMatch!)
                    .DistinctBy(match => match.ContainerPath + "|" + match.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                extractionRoot,
                MaxExtractionBytes);

            foreach (var warning in extraction.Warnings)
            {
                messages.Add(warning);
                auditTrail.Add(BuildAudit("extract-warning", warning));
            }

            foreach (var file in extraction.Files)
            {
                extractedByKey[file.ContainerPath + "|" + file.RelativePath] = file.ExtractedPath;
            }
        }

        foreach (var plan in report.AssetPlans.Where(plan => plan.Required && plan.NeedsCopy))
        {
            var fullTargetPath = Path.GetFullPath(plan.TargetPath);
            var existedBefore = File.Exists(fullTargetPath);
            var backupPath = BuildBackupPath(backupRoot, fullTargetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

            if (existedBefore)
            {
                File.Copy(fullTargetPath, backupPath, overwrite: true);
                auditTrail.Add(BuildAudit("backup", $"Backup created at {backupPath}.", fullTargetPath));
            }

            var sourcePath = ResolveAssetSourcePath(plan, extractedByKey);
            var previousBytes = existedBefore ? File.ReadAllBytes(fullTargetPath) : [];
            var newBytes = File.ReadAllBytes(sourcePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)!);
            WriteBinaryAtomically(fullTargetPath, newBytes);
            auditTrail.Add(BuildAudit("write", $"Copied map asset '{plan.RelativePath}' from {plan.SourceKind}.", fullTargetPath));

            var snapshot = new MapAppliedFileSnapshot(
                fullTargetPath,
                backupPath,
                existedBefore,
                "copy",
                true,
                previousBytes.LongLength,
                newBytes.LongLength,
                existedBefore ? ComputeSha256(previousBytes) : null,
                ComputeSha256(newBytes),
                existedBefore && File.Exists(backupPath) ? ComputeSha256(File.ReadAllBytes(backupPath)) : null,
                BuildSourceSummary(plan));
            files.Add(snapshot);
            ValidateWrittenFile(snapshot);
            auditTrail.Add(BuildAudit("postvalidate", "Written map asset hash matches the apply manifest.", fullTargetPath));
        }
    }

    private void ApplyMapCache(
        RepositoryPaths repositoryPaths,
        MapDryRunReport report,
        string backupRoot,
        string operationTempRoot,
        IList<MapAppliedFileSnapshot> files,
        IList<ItemApplyAuditEntry> auditTrail,
        IList<string> messages)
    {
        var mapCachePlan = report.MapCachePlan
                           ?? throw new InvalidOperationException("Map apply requires a map cache plan.");
        var finalMapCachePath = Path.GetFullPath(mapCachePlan.CachePath);
        var cacheWorkingRoot = Path.Combine(operationTempRoot, "mapcache");
        var stagedMapCachePath = Path.Combine(cacheWorkingRoot, "map_cache.staging.dat");

        var buildResult = _mapCacheBuilder.Build(
            repositoryPaths,
            new MapCacheBuildRequest(
                report.Input.MapName,
                finalMapCachePath,
                stagedMapCachePath,
                cacheWorkingRoot));
        foreach (var message in buildResult.Messages)
        {
            messages.Add(message);
            auditTrail.Add(BuildAudit("mapcache", message, finalMapCachePath));
        }

        if (!buildResult.Succeeded)
        {
            throw new InvalidOperationException("Map cache build failed.");
        }

        var existedBefore = File.Exists(finalMapCachePath);
        var backupPath = BuildBackupPath(backupRoot, finalMapCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        if (existedBefore)
        {
            File.Copy(finalMapCachePath, backupPath, overwrite: true);
            auditTrail.Add(BuildAudit("backup", $"Backup created at {backupPath}.", finalMapCachePath));
        }

        var previousBytes = existedBefore ? File.ReadAllBytes(finalMapCachePath) : [];
        var newBytes = File.ReadAllBytes(stagedMapCachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(finalMapCachePath)!);
        WriteBinaryAtomically(finalMapCachePath, newBytes);
        auditTrail.Add(BuildAudit("write", "Rebuilt map_cache.dat from staging output.", finalMapCachePath));

        var snapshot = new MapAppliedFileSnapshot(
            finalMapCachePath,
            backupPath,
            existedBefore,
            "rebuild",
            true,
            previousBytes.LongLength,
            newBytes.LongLength,
            existedBefore ? ComputeSha256(previousBytes) : null,
            ComputeSha256(newBytes),
            existedBefore && File.Exists(backupPath) ? ComputeSha256(File.ReadAllBytes(backupPath)) : null,
            $"{Path.GetFileName(buildResult.ToolPath)} exit {buildResult.ExitCode}");
        files.Add(snapshot);
        ValidateWrittenFile(snapshot);
        auditTrail.Add(BuildAudit("postvalidate", "Written map cache hash matches the apply manifest.", finalMapCachePath));
    }

    private static IReadOnlyList<ItemApplyConflict> AnalyzeMapRegistrationConflicts(
        RepositoryPaths repositoryPaths,
        MapDryRunReport report)
    {
        var conflicts = new List<ItemApplyConflict>();
        var mapIndexPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "db", "import", "map_index.txt");
        if (File.Exists(mapIndexPath)
            && File.ReadLines(mapIndexPath).Any(line =>
                line.TrimStart().StartsWith(report.Input.MapName + " ", StringComparison.OrdinalIgnoreCase)
                || line.Trim().Equals(report.Input.MapName, StringComparison.OrdinalIgnoreCase)))
        {
            conflicts.Add(new ItemApplyConflict(
                mapIndexPath,
                "append",
                "map.index-present",
                $"Map '{report.Input.MapName}' is already present in map_index.txt.",
                report.Input.MapName));
        }

        var mapsAthenaPath = Path.Combine(Path.GetFullPath(repositoryPaths.RathenaPath), "conf", "maps_athena.conf");
        if (File.Exists(mapsAthenaPath)
            && File.ReadLines(mapsAthenaPath).Any(line => line.Trim().Equals($"map: {report.Input.MapName}", StringComparison.OrdinalIgnoreCase)))
        {
            conflicts.Add(new ItemApplyConflict(
                mapsAthenaPath,
                "append",
                "map.loader-present",
                $"Map '{report.Input.MapName}' is already present in maps_athena.conf.",
                "map: " + report.Input.MapName));
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ItemApplyConflict> AnalyzeTextConflicts(
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
                "Target map file already exists; create operation would collide.",
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
                    "Target already contains the exact map preview text.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(normalizedPreview)
                && normalizedPrevious.Contains(normalizedPreview, StringComparison.Ordinal))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.normalized-preview-present",
                    "Target already contains the proposed map content after newline normalization.",
                    firstPreviewLine));
            }

            if (!string.IsNullOrWhiteSpace(firstPreviewLine)
                && normalizedPrevious.Split('\n').Any(line => line.Trim().Equals(firstPreviewLine.Trim(), StringComparison.Ordinal)))
            {
                conflicts.Add(new ItemApplyConflict(
                    fullTargetPath,
                    change.ChangeKind,
                    "append.anchor-line-present",
                    "Target already contains the first meaningful preview line; append may duplicate an existing map block.",
                    firstPreviewLine));
            }
        }

        return conflicts
            .DistinctBy(conflict => $"{conflict.TargetPath}|{conflict.Code}|{conflict.Evidence}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool CanResolveAssetSource(MapAssetPlan plan) =>
        string.Equals(plan.SourceKind, "LoosePatch", StringComparison.OrdinalIgnoreCase)
            ? !string.IsNullOrWhiteSpace(plan.SourcePath) && File.Exists(plan.SourcePath)
            : string.Equals(plan.SourceKind, "GrfExtraction", StringComparison.OrdinalIgnoreCase) && plan.GrfMatch is not null;

    private static string ResolveAssetSourcePath(
        MapAssetPlan plan,
        IReadOnlyDictionary<string, string> extractedByKey)
    {
        if (string.Equals(plan.SourceKind, "LoosePatch", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(plan.SourcePath) && File.Exists(plan.SourcePath))
            {
                return plan.SourcePath;
            }

            throw new InvalidOperationException($"Loose source for map asset '{plan.RelativePath}' is unavailable.");
        }

        if (string.Equals(plan.SourceKind, "GrfExtraction", StringComparison.OrdinalIgnoreCase) && plan.GrfMatch is not null)
        {
            var key = plan.GrfMatch.ContainerPath + "|" + plan.GrfMatch.RelativePath;
            if (extractedByKey.TryGetValue(key, out var extractedPath) && File.Exists(extractedPath))
            {
                return extractedPath;
            }

            throw new InvalidOperationException($"Extracted source for map asset '{plan.RelativePath}' is unavailable.");
        }

        throw new InvalidOperationException($"Unsupported map asset source kind '{plan.SourceKind}'.");
    }

    private MapApplyLog BuildLog(
        string operationId,
        string applyLogPath,
        string backupRoot,
        RepositoryPaths repositoryPaths,
        MapDryRunReport report,
        string status,
        IReadOnlyList<MapAppliedFileSnapshot> files,
        IReadOnlyList<ItemApplyConflict> conflicts,
        IReadOnlyList<ItemApplyAuditEntry> auditTrail,
        IReadOnlyList<string> messages) =>
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
            files,
            conflicts,
            auditTrail,
            messages,
            report.AssetPlans,
            report.MapCachePlan);

    private static MapApplyResult BuildResult(
        string operationId,
        bool applied,
        string applyLogPath,
        string backupRoot,
        MapDryRunReport report,
        IReadOnlyList<MapAppliedFileSnapshot> files,
        IReadOnlyList<ItemApplyConflict> conflicts,
        IReadOnlyList<ItemApplyAuditEntry> auditTrail,
        IReadOnlyList<string> messages) =>
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
            messages);

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

    private static void WriteTextAtomically(string targetPath, string content)
    {
        var tempPath = targetPath + ".ragnaforge.tmp";
        File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    private static void WriteBinaryAtomically(string targetPath, byte[] content)
    {
        var tempPath = targetPath + ".ragnaforge.tmp";
        File.WriteAllBytes(tempPath, content);

        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    private static void ValidateWrittenFile(MapAppliedFileSnapshot file)
    {
        if (!File.Exists(file.TargetPath))
        {
            throw new InvalidOperationException($"Map target was not written: {file.TargetPath}");
        }

        var currentSha = ComputeSha256(File.ReadAllBytes(file.TargetPath));
        if (!currentSha.Equals(file.NewSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Map target hash mismatch after write: {file.TargetPath}");
        }
    }

    private void TryRollbackFiles(IReadOnlyList<MapAppliedFileSnapshot> files)
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

    private static string BuildSourceSummary(MapAssetPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.SourcePath))
        {
            return $"{plan.SourceKind}: {plan.SourcePath}";
        }

        if (plan.GrfMatch is not null)
        {
            return $"{plan.SourceKind}: {plan.GrfMatch.ContainerPath}::{plan.GrfMatch.RelativePath}";
        }

        return plan.SourceKind;
    }

    private static void EnsureAllowedTarget(string fullTargetPath, IReadOnlyList<string> allowedRoots)
    {
        if (!allowedRoots.Any(root => IsPathInsideRoot(root, fullTargetPath)))
        {
            throw new InvalidOperationException($"Refusing to write outside map roots: {fullTargetPath}");
        }
    }

    private void EnsurePathInsideWorkspace(string fullPath)
    {
        if (!IsPathInsideRoot(workspaceRoot, fullPath))
        {
            throw new InvalidOperationException($"Refusing to use a path outside the workspace: {fullPath}");
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

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string? FirstMeaningfulLine(string value) =>
        NormalizeNewLines(value)
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));

    private static string ComputeSha256(byte[] content)
    {
        var hash = SHA256.HashData(content);
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
