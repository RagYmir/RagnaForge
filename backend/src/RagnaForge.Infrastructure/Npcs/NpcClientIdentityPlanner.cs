using System.Text.RegularExpressions;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Npcs;
using RagnaForge.Infrastructure.FileSystem;
using RagnaForge.Infrastructure.Items;
using RagnaForge.Infrastructure.Patch;

namespace RagnaForge.Infrastructure.Npcs;

internal sealed class NpcClientIdentityPlanner(
    IGrfAssetLookupService? grfAssetLookupService,
    GrfAssetLookupOptions grfAssetLookupOptions)
{
    private static readonly string[] SpriteAssetExtensions = [".spr", ".act"];
    private static readonly string[] LogicalFiles = ["jobname", "jobidentity", "npcidentity"];

    private readonly IGrfAssetLookupService? _grfAssetLookupService = grfAssetLookupService;
    private readonly GrfAssetLookupOptions _grfAssetLookupOptions = grfAssetLookupOptions;
    private readonly LuaScriptFormatDetector _formatDetector = new();

    public NpcClientIdentityPlanningResult Create(RepositoryPaths paths, NpcDefinitionInput input)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(input);

        var validationWarnings = new List<string>();
        var validationErrors = new List<string>();
        var dependencies = new List<ItemDependency>();
        var proposedChanges = new List<ProposedFileChange>();

        var spriteValidation = ValidateSprite(paths.PatchPath, paths, input.Sprite, validationWarnings);
        var spriteResolution = BuildSpriteResolution(input.Sprite, spriteValidation);
        var detectedFiles = DetectClientIdentityFiles(paths.PatchPath, validationWarnings, validationErrors);
        var selectedFiles = detectedFiles
            .Where(file => file.Selected)
            .ToDictionary(file => file.LogicalName, StringComparer.OrdinalIgnoreCase);

        var normalizedClientSymbol = NormalizeClientSymbol(input.ClientSymbolName);
        if (!string.IsNullOrWhiteSpace(input.ClientSymbolName) && normalizedClientSymbol is null)
        {
            validationErrors.Add($"Client symbol '{input.ClientSymbolName}' is not safe for JT_/jobtbl registration.");
        }

        if (input.ClientIdentityId is <= 0)
        {
            validationErrors.Add("Client identity ID must be greater than zero when provided.");
        }

        var readableFiles = LoadReadableFiles(selectedFiles.Values, validationWarnings, validationErrors);
        var existingState = AnalyzeExistingRegistration(
            input,
            normalizedClientSymbol,
            readableFiles,
            validationWarnings,
            validationErrors);

        var requiresClientIdentity = RequiresClientIdentity(spriteValidation, existingState);
        var blockReasons = new List<string>();
        var unsupportedFiles = new List<string>();
        var bytecodeBlockedFiles = new List<string>();
        var existingRegistration = existingState.Details
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var proposedRegistrations = new List<string>();

        foreach (var file in detectedFiles)
        {
            if (!file.Selected)
            {
                continue;
            }

            switch (file.Format)
            {
                case NpcClientFileFormat.BinaryLub:
                    bytecodeBlockedFiles.Add(file.Path ?? file.LogicalName);
                    break;
                case NpcClientFileFormat.Unknown:
                    unsupportedFiles.Add(file.Path ?? file.LogicalName);
                    break;
            }
        }

        if (requiresClientIdentity)
        {
            if (spriteResolution.Ambiguous)
            {
                blockReasons.Add("NPC sprite resolution is ambiguous; choose an explicit sprite source before applying client identity.");
            }
            else if (!spriteResolution.Resolved)
            {
                blockReasons.Add("NPC sprite could not be resolved in Patch or GRF; client identity cannot be applied safely.");
            }
            else if (spriteResolution.NeedsAssetCopyPlan)
            {
                blockReasons.Add("NPC sprite was resolved only in GRF; Patch asset copy is still pending, so full client identity apply stays blocked.");
            }

            foreach (var file in detectedFiles.Where(file => file.Selected))
            {
                if (file.Format == NpcClientFileFormat.Missing)
                {
                    blockReasons.Add($"Required client identity file '{file.LogicalName}' was not found in the Patch.");
                }
                else if (file.Format == NpcClientFileFormat.BinaryLub)
                {
                    blockReasons.Add($"Client identity file '{file.Path}' is Lua bytecode/binary and is blocked in this milestone.");
                }
                else if (!file.SupportedForApply)
                {
                    blockReasons.Add($"Client identity file '{file.Path ?? file.LogicalName}' uses unsupported format '{file.Format}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(normalizedClientSymbol))
            {
                blockReasons.Add($"Client symbol is required for custom NPC registration. Suggested symbol: {BuildSuggestedSymbol(input.Sprite)}.");
            }

            if (input.ClientIdentityId is null)
            {
                blockReasons.Add("Client identity ID is required for custom NPC registration.");
            }

            if (existingState.HasConflicts)
            {
                blockReasons.AddRange(existingState.Errors);
            }

            if (blockReasons.Count == 0
                && normalizedClientSymbol is not null
                && input.ClientIdentityId is not null)
            {
                foreach (var logicalName in LogicalFiles)
                {
                    if (!selectedFiles.TryGetValue(logicalName, out var file)
                        || !readableFiles.TryGetValue(logicalName, out var readable))
                    {
                        continue;
                    }

                    var preview = BuildRegistrationPreview(
                        logicalName,
                        file.Format,
                        readable.Content,
                        normalizedClientSymbol,
                        input.ClientIdentityId.Value,
                        input.Sprite,
                        blockReasons);
                    if (preview is null)
                    {
                        continue;
                    }

                    proposedChanges.Add(new ProposedFileChange(
                        readable.Path,
                        "append",
                        true,
                        preview));
                    proposedRegistrations.Add($"{Path.GetFileName(readable.Path)} => {FirstPreviewLine(preview)}");
                }
            }
        }

        var diffPreviewEntries = ItemDiffPreviewBuilder
            .Build(proposedChanges)
            .Entries;
        var applyTargets = proposedChanges
            .Select(change => change.TargetPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var postWriteValidationPlan = applyTargets
            .Select(target => new PostWriteValidationPlanEntry(
                target,
                Path.GetExtension(target).Equals(".txt", StringComparison.OrdinalIgnoreCase)
                    ? "LegacyClientIdentityTxtValidator"
                    : "LuaTextValidator",
                "Validate final NPC client identity file in staging before replacement."))
            .ToArray();

        var canApplyClientIdentity = requiresClientIdentity
            && blockReasons.Count == 0
            && validationErrors.Count == 0
            && proposedChanges.Count > 0;
        var plan = new NpcClientIdentityPlan(
            requiresClientIdentity,
            canApplyClientIdentity,
            blockReasons
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            detectedFiles,
            detectedFiles
                .Select(file => $"{file.LogicalName}:{file.Format}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            input.Sprite,
            spriteResolution.Resolved,
            spriteResolution.Source,
            spriteResolution.Path,
            existingState.CompleteRegistration,
            existingRegistration,
            proposedRegistrations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            unsupportedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            bytecodeBlockedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            validationWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            validationErrors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            diffPreviewEntries,
            applyTargets,
            applyTargets,
            postWriteValidationPlan);

        dependencies.AddRange(BuildSpriteDependencies(spriteValidation, spriteResolution));
        dependencies.AddRange(BuildClientIdentityDependencies(plan));

        return new NpcClientIdentityPlanningResult(
            spriteValidation,
            spriteResolution,
            plan,
            dependencies,
            proposedChanges);
    }

    private IEnumerable<ItemDependency> BuildSpriteDependencies(NpcSpriteValidation validation, NpcSpriteResolution resolution)
    {
        if (validation.IsStandardClientSprite)
        {
            yield return new ItemDependency(
                "NpcSprite",
                ItemDependencyState.Satisfied,
                $"NPC sprite '{validation.Sprite}' was confirmed in client datainfo via {validation.DetectionSource}.",
                resolution.Path ?? validation.Evidence.FirstOrDefault());
            yield break;
        }

        if (resolution.Ambiguous)
        {
            yield return new ItemDependency(
                "NpcSprite",
                ItemDependencyState.Missing,
                $"NPC sprite '{validation.Sprite}' matched multiple client asset candidates and needs explicit disambiguation.",
                resolution.Candidates.FirstOrDefault());
            yield break;
        }

        if (resolution.Resolved && resolution.NeedsAssetCopyPlan)
        {
            yield return new ItemDependency(
                "NpcSprite",
                ItemDependencyState.Warning,
                $"NPC sprite '{validation.Sprite}' was resolved via {resolution.Source}, but Patch asset copy is still pending.",
                resolution.Path);
            yield break;
        }

        if (resolution.Resolved)
        {
            yield return new ItemDependency(
                "NpcSprite",
                ItemDependencyState.Satisfied,
                $"NPC sprite '{validation.Sprite}' is available for client validation via {resolution.Source}.",
                resolution.Path);
            yield break;
        }

        yield return new ItemDependency(
            "NpcSprite",
            validation.RequiresAdditionalClientValidation ? ItemDependencyState.Warning : ItemDependencyState.Missing,
            $"NPC sprite '{validation.Sprite}' could not be confirmed in client datainfo or custom sprite assets.");
    }

    private static IEnumerable<ItemDependency> BuildClientIdentityDependencies(NpcClientIdentityPlan plan)
    {
        foreach (var file in plan.FilesDetected.Where(file => file.Selected))
        {
            yield return file.Format switch
            {
                NpcClientFileFormat.TextLua or NpcClientFileFormat.TextLub => new ItemDependency(
                    "Patch",
                    ItemDependencyState.Satisfied,
                    $"Client identity file is readable and safe for staged diff/apply.",
                    file.Path),
                NpcClientFileFormat.LegacyTxt => new ItemDependency(
                    "Patch",
                    ItemDependencyState.Satisfied,
                    $"Legacy TXT client identity file was detected and can be staged with the recognized format.",
                    file.Path),
                NpcClientFileFormat.BinaryLub => new ItemDependency(
                    "Patch",
                    ItemDependencyState.Missing,
                    $"Client identity file is Lua bytecode/binary and is blocked in this milestone.",
                    file.Path),
                NpcClientFileFormat.Missing => new ItemDependency(
                    "Patch",
                    ItemDependencyState.Missing,
                    $"Required client identity file '{file.LogicalName}' was not found.",
                    file.Path),
                _ => new ItemDependency(
                    "Patch",
                    ItemDependencyState.Missing,
                    $"Client identity file '{file.LogicalName}' uses unsupported format '{file.Format}'.",
                    file.Path)
            };
        }

        if (!plan.Required)
        {
            yield return new ItemDependency(
                "NpcClientIdentity",
                ItemDependencyState.Satisfied,
                plan.ExistingRegistration
                    ? "NPC client identity is already registered."
                    : "NPC client identity update is not required for this sprite.");
            yield break;
        }

        if (plan.CanApply)
        {
            yield return new ItemDependency(
                "NpcClientIdentity",
                ItemDependencyState.Satisfied,
                $"Prepared {plan.ProposedRegistrations.Count} client identity registration(s) for NPC sprite '{plan.SpriteName}'.");
            yield break;
        }

        foreach (var reason in plan.BlockReasons)
        {
            yield return new ItemDependency(
                "NpcClientIdentity",
                ItemDependencyState.Missing,
                reason);
        }
    }

    private NpcSpriteValidation ValidateSprite(
        string patchPath,
        RepositoryPaths repositoryPaths,
        string sprite,
        List<string> warnings)
    {
        if (Regex.IsMatch(sprite, @"^-?\d+$", RegexOptions.CultureInvariant))
        {
            return new NpcSpriteValidation(
                sprite,
                false,
                true,
                "numeric-sprite",
                [],
                null);
        }

        var datainfoRoot = SafeFileSystem.Combine(patchPath, "data", "luafiles514", "lua files", "datainfo");
        var jobNamePath = SafeFileSystem.Combine(datainfoRoot, "jobname.lub");
        var jobIdentityPath = SafeFileSystem.Combine(datainfoRoot, "jobidentity.lub");
        var npcIdentityPath = SafeFileSystem.Combine(datainfoRoot, "npcidentity.lub");
        var symbol = sprite.StartsWith("JT_", StringComparison.OrdinalIgnoreCase)
            ? sprite
            : "JT_" + sprite;

        var readableJobName = TryReadLuaText(jobNamePath, warnings);
        var readableJobIdentity = TryReadLuaText(jobIdentityPath, warnings);
        var readableNpcIdentity = TryReadLuaText(npcIdentityPath, warnings);

        var hasJobName = readableJobName?.Contains($"\"{sprite}\"", StringComparison.OrdinalIgnoreCase) == true;
        var hasIdentity = (readableJobIdentity?.Contains(symbol, StringComparison.OrdinalIgnoreCase) == true)
                          || (readableNpcIdentity?.Contains(symbol, StringComparison.OrdinalIgnoreCase) == true);

        if (hasJobName && hasIdentity)
        {
            return new NpcSpriteValidation(
                sprite,
                true,
                false,
                "jobname/jobidentity",
                [jobNamePath, jobIdentityPath, npcIdentityPath],
                null);
        }

        var looseCandidates = FindLooseSpriteCandidates(patchPath, sprite);
        if (looseCandidates.Count > 0)
        {
            return new NpcSpriteValidation(
                sprite,
                false,
                true,
                "loose-custom-sprite",
                looseCandidates,
                null);
        }

        var grfLookup = ResolveSpriteAssetLookup(repositoryPaths, sprite);
        foreach (var warning in grfLookup?.Warnings ?? [])
        {
            warnings.Add(warning);
        }

        return new NpcSpriteValidation(
            sprite,
            false,
            true,
            grfLookup is { Matches.Count: > 0 } ? BuildGrfDetectionSource(grfLookup) : "unresolved",
            [],
            grfLookup);
    }

    private NpcSpriteResolution BuildSpriteResolution(string sprite, NpcSpriteValidation validation)
    {
        if (validation.DetectionSource.Equals("numeric-sprite", StringComparison.OrdinalIgnoreCase))
        {
            return new NpcSpriteResolution(sprite, true, false, validation.DetectionSource, null, [], false);
        }

        if (validation.IsStandardClientSprite)
        {
            return new NpcSpriteResolution(
                sprite,
                true,
                false,
                validation.DetectionSource,
                validation.Evidence.FirstOrDefault(),
                validation.Evidence,
                false);
        }

        if (validation.Evidence.Count > 0)
        {
            var candidates = validation.Evidence
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new NpcSpriteResolution(
                sprite,
                true,
                false,
                validation.DetectionSource,
                candidates.FirstOrDefault(),
                candidates,
                false);
        }

        if (validation.AssetLookup is { Matches.Count: > 0 } lookup)
        {
            var groupedCandidates = lookup.Matches
                .GroupBy(
                    match => $"{match.ContainerPath}::{Path.ChangeExtension(match.RelativePath, null)}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (groupedCandidates.Length == 1)
            {
                return new NpcSpriteResolution(
                    sprite,
                    true,
                    false,
                    BuildGrfDetectionSource(lookup),
                    groupedCandidates[0],
                    groupedCandidates,
                    true);
            }

            return new NpcSpriteResolution(
                sprite,
                false,
                true,
                BuildGrfDetectionSource(lookup),
                null,
                groupedCandidates,
                true);
        }

        return new NpcSpriteResolution(sprite, false, false, validation.DetectionSource, null, [], false);
    }

    private IReadOnlyList<NpcClientIdentityFileDetection> DetectClientIdentityFiles(
        string patchPath,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var detections = new List<NpcClientIdentityFileDetection>();

        foreach (var logicalName in LogicalFiles)
        {
            var candidates = DetectCandidatesForLogicalFile(patchPath, logicalName).ToArray();
            if (candidates.Length == 0)
            {
                detections.Add(new NpcClientIdentityFileDetection(
                    logicalName,
                    null,
                    NpcClientFileFormat.Missing,
                    true,
                    false));
                continue;
            }

            var orderedCandidates = candidates
                .OrderBy(candidate => CandidatePriority(candidate.Path ?? string.Empty, candidate.Format, logicalName))
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var selected = orderedCandidates[0];
            var selectedPriority = CandidatePriority(selected.Path ?? string.Empty, selected.Format, logicalName);
            var tied = orderedCandidates
                .Where(candidate => CandidatePriority(candidate.Path ?? string.Empty, candidate.Format, logicalName) == selectedPriority)
                .ToArray();
            if (tied.Length > 1)
            {
                errors.Add($"Multiple candidate files were detected for '{logicalName}' with the same precedence: {string.Join(", ", tied.Select(candidate => candidate.Path))}.");
            }

            foreach (var candidate in orderedCandidates)
            {
                detections.Add(candidate with
                {
                    Selected = string.Equals(candidate.Path, selected.Path, StringComparison.OrdinalIgnoreCase)
                });
            }

            if (orderedCandidates.Length > 1)
            {
                warnings.Add($"Multiple candidate files were detected for '{logicalName}'. Selected '{selected.Path}' for the current plan.");
            }
        }

        return detections;
    }

    private IEnumerable<NpcClientIdentityFileDetection> DetectCandidatesForLogicalFile(string patchPath, string logicalName)
    {
        if (!Directory.Exists(patchPath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(patchPath, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!fileName.Equals(logicalName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(file);
            if (!extension.Equals(".lua", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".lub", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var format = ClassifyClientIdentityFormat(file, extension);
            yield return new NpcClientIdentityFileDetection(
                logicalName,
                file,
                format,
                false,
                format is NpcClientFileFormat.TextLua or NpcClientFileFormat.TextLub or NpcClientFileFormat.LegacyTxt);
        }
    }

    private NpcClientFileFormat ClassifyClientIdentityFormat(string path, string extension)
    {
        if (!File.Exists(path))
        {
            return NpcClientFileFormat.Missing;
        }

        if (extension.Equals(".lua", StringComparison.OrdinalIgnoreCase))
        {
            var inspection = _formatDetector.Inspect(path);
            return inspection.ReadableAsText ? NpcClientFileFormat.TextLua : NpcClientFileFormat.Unknown;
        }

        if (extension.Equals(".lub", StringComparison.OrdinalIgnoreCase))
        {
            var inspection = _formatDetector.Inspect(path);
            return inspection.Format switch
            {
                LuaScriptFormat.Text => NpcClientFileFormat.TextLub,
                LuaScriptFormat.LuaBytecode or LuaScriptFormat.BinaryUnknown => NpcClientFileFormat.BinaryLub,
                LuaScriptFormat.Missing => NpcClientFileFormat.Missing,
                _ => NpcClientFileFormat.Unknown
            };
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return NpcClientFileFormat.LegacyTxt;
        }

        return NpcClientFileFormat.Unknown;
    }

    private static int CandidatePriority(string path, NpcClientFileFormat format, string logicalName)
    {
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToLowerInvariant();
        var datainfo = $"{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}luafiles514{Path.DirectorySeparatorChar}lua files{Path.DirectorySeparatorChar}datainfo{Path.DirectorySeparatorChar}{logicalName}";
        var luaFiles = $"{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}luafiles514{Path.DirectorySeparatorChar}lua files{Path.DirectorySeparatorChar}";
        var data = $"{Path.DirectorySeparatorChar}data{Path.DirectorySeparatorChar}";
        var system = $"{Path.DirectorySeparatorChar}system{Path.DirectorySeparatorChar}";

        var pathScore = normalized.Contains(datainfo, StringComparison.Ordinal) ? 0
            : normalized.Contains(luaFiles, StringComparison.Ordinal) ? 1
            : normalized.Contains(data, StringComparison.Ordinal) ? 2
            : normalized.Contains(system, StringComparison.Ordinal) ? 3
            : 4;
        var formatScore = format switch
        {
            NpcClientFileFormat.TextLub => 0,
            NpcClientFileFormat.BinaryLub => 1,
            NpcClientFileFormat.TextLua => 2,
            NpcClientFileFormat.LegacyTxt => 3,
            NpcClientFileFormat.Unknown => 4,
            _ => 5
        };

        return (pathScore * 10) + formatScore;
    }

    private static Dictionary<string, ReadableClientIdentityFile> LoadReadableFiles(
        IReadOnlyCollection<NpcClientIdentityFileDetection> files,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var readable = new Dictionary<string, ReadableClientIdentityFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.Where(file => file.Selected))
        {
            if (file.Path is null)
            {
                continue;
            }

            if (file.Format is NpcClientFileFormat.TextLua or NpcClientFileFormat.TextLub or NpcClientFileFormat.LegacyTxt)
            {
                readable[file.LogicalName] = new ReadableClientIdentityFile(
                    file.LogicalName,
                    file.Path,
                    file.Format,
                    File.ReadAllText(file.Path),
                    ProbeLegacyTxtStyle(file.Path, file.Format));
            }
            else if (file.Format == NpcClientFileFormat.BinaryLub)
            {
                warnings.Add($"{Path.GetFileName(file.Path)} is Lua bytecode/binary and will stay read-only.");
            }
            else if (file.Format == NpcClientFileFormat.Unknown)
            {
                errors.Add($"{Path.GetFileName(file.Path)} could not be classified as a safe text identity file.");
            }
        }

        return readable;
    }

    private static string? ProbeLegacyTxtStyle(string path, NpcClientFileFormat format)
    {
        if (format != NpcClientFileFormat.LegacyTxt)
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Contains('='))
            {
                return "=";
            }

            if (trimmed.Contains(','))
            {
                return ",";
            }
        }

        return "=";
    }

    private static ExistingClientIdentityState AnalyzeExistingRegistration(
        NpcDefinitionInput input,
        string? normalizedClientSymbol,
        IReadOnlyDictionary<string, ReadableClientIdentityFile> readableFiles,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var details = new List<string>();
        var stateErrors = new List<string>();

        readableFiles.TryGetValue("jobname", out var jobNameFile);
        readableFiles.TryGetValue("jobidentity", out var jobIdentityFile);
        readableFiles.TryGetValue("npcidentity", out var npcIdentityFile);

        var jobNameSymbol = jobNameFile is null ? null : FindJobNameSymbolBySprite(jobNameFile, input.Sprite);
        if (jobNameSymbol is not null)
        {
            details.Add($"{Path.GetFileName(jobNameFile!.Path)} already maps sprite '{input.Sprite}' to symbol '{jobNameSymbol}'.");
        }

        var resolvedSymbol = normalizedClientSymbol ?? jobNameSymbol;
        if (normalizedClientSymbol is not null && jobNameSymbol is not null && !normalizedClientSymbol.Equals(jobNameSymbol, StringComparison.OrdinalIgnoreCase))
        {
            stateErrors.Add($"Sprite '{input.Sprite}' is already registered under symbol '{jobNameSymbol}', not '{normalizedClientSymbol}'.");
        }

        if (resolvedSymbol is not null && jobNameFile is not null)
        {
            var spriteBySymbol = FindJobNameSpriteBySymbol(jobNameFile, resolvedSymbol);
            if (spriteBySymbol is not null)
            {
                details.Add($"{Path.GetFileName(jobNameFile.Path)} already contains symbol '{resolvedSymbol}' => '{spriteBySymbol}'.");
                if (!spriteBySymbol.Equals(input.Sprite, StringComparison.OrdinalIgnoreCase))
                {
                    stateErrors.Add($"Client symbol '{resolvedSymbol}' is already mapped to sprite '{spriteBySymbol}'.");
                }
            }
        }

        var jobIdentityId = resolvedSymbol is null || jobIdentityFile is null
            ? null
            : FindIdentityValueBySymbol(jobIdentityFile, resolvedSymbol);
        if (resolvedSymbol is not null && jobIdentityId is not null)
        {
            details.Add($"{Path.GetFileName(jobIdentityFile!.Path)} already maps '{resolvedSymbol}' to {jobIdentityId}.");
        }

        var npcIdentityId = resolvedSymbol is null || npcIdentityFile is null
            ? null
            : FindIdentityValueBySymbol(npcIdentityFile, resolvedSymbol);
        if (resolvedSymbol is not null && npcIdentityId is not null)
        {
            details.Add($"{Path.GetFileName(npcIdentityFile!.Path)} already maps '{resolvedSymbol}' to {npcIdentityId}.");
        }

        if (resolvedSymbol is not null && input.ClientIdentityId is not null)
        {
            if (jobIdentityId is not null && jobIdentityId != input.ClientIdentityId)
            {
                stateErrors.Add($"Client symbol '{resolvedSymbol}' already uses identity ID {jobIdentityId} in jobidentity, not {input.ClientIdentityId}.");
            }

            if (npcIdentityId is not null && npcIdentityId != input.ClientIdentityId)
            {
                stateErrors.Add($"Client symbol '{resolvedSymbol}' already uses identity ID {npcIdentityId} in npcidentity, not {input.ClientIdentityId}.");
            }
        }

        if (resolvedSymbol is not null && jobIdentityId is not null && npcIdentityId is not null && jobIdentityId != npcIdentityId)
        {
            stateErrors.Add($"Client symbol '{resolvedSymbol}' has inconsistent IDs between jobidentity ({jobIdentityId}) and npcidentity ({npcIdentityId}).");
        }

        if (input.ClientIdentityId is not null)
        {
            if (jobIdentityFile is not null)
            {
                var symbolById = FindSymbolByIdentityValue(jobIdentityFile, input.ClientIdentityId.Value);
                if (symbolById is not null && !symbolById.Equals(resolvedSymbol, StringComparison.OrdinalIgnoreCase))
                {
                    stateErrors.Add($"Client identity ID {input.ClientIdentityId} is already used by symbol '{symbolById}' in {Path.GetFileName(jobIdentityFile.Path)}.");
                }
            }

            if (npcIdentityFile is not null)
            {
                var symbolById = FindSymbolByIdentityValue(npcIdentityFile, input.ClientIdentityId.Value);
                if (symbolById is not null && !symbolById.Equals(resolvedSymbol, StringComparison.OrdinalIgnoreCase))
                {
                    stateErrors.Add($"Client identity ID {input.ClientIdentityId} is already used by symbol '{symbolById}' in {Path.GetFileName(npcIdentityFile.Path)}.");
                }
            }
        }

        if (stateErrors.Count == 0 && jobNameSymbol is not null && jobIdentityId is not null && npcIdentityId is not null)
        {
            details.Add($"Existing client identity registration is complete for sprite '{input.Sprite}'.");
        }
        else if (jobNameSymbol is not null || jobIdentityId is not null || npcIdentityId is not null)
        {
            warnings.Add($"Client identity registration for sprite '{input.Sprite}' looks partial and was kept under review.");
        }

        foreach (var error in stateErrors)
        {
            errors.Add(error);
        }

        return new ExistingClientIdentityState(
            jobNameSymbol is not null && jobIdentityId is not null && npcIdentityId is not null && stateErrors.Count == 0,
            stateErrors.Count > 0,
            details,
            stateErrors);
    }

    private static bool RequiresClientIdentity(NpcSpriteValidation spriteValidation, ExistingClientIdentityState existingState)
    {
        if (spriteValidation.DetectionSource.Equals("numeric-sprite", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (spriteValidation.IsStandardClientSprite)
        {
            return false;
        }

        return !existingState.CompleteRegistration;
    }

    private static string? BuildRegistrationPreview(
        string logicalName,
        NpcClientFileFormat format,
        string existingContent,
        string symbol,
        int identityId,
        string spriteName,
        ICollection<string> blockReasons)
    {
        if (format is NpcClientFileFormat.TextLua or NpcClientFileFormat.TextLub)
        {
            return logicalName switch
            {
                "jobname" => $"\nJobNameTable[jobtbl.{symbol}] = \"{spriteName}\"",
                "jobidentity" => $"\nJTtbl.{symbol} = {identityId}",
                "npcidentity" => $"\njobtbl.{symbol} = {identityId}",
                _ => null
            };
        }

        if (format == NpcClientFileFormat.LegacyTxt)
        {
            var style = ProbeLegacyTxtStyleFromContent(existingContent);
            if (style is null)
            {
                blockReasons.Add($"Legacy TXT format for '{logicalName}' could not be recognized safely.");
                return null;
            }

            var value = logicalName.Equals("jobname", StringComparison.OrdinalIgnoreCase)
                ? spriteName
                : identityId.ToString();
            return style == ","
                ? $"\n{symbol},{value}"
                : $"\n{symbol}={value}";
        }

        blockReasons.Add($"Unsupported client identity format '{format}' for '{logicalName}'.");
        return null;
    }

    private static string? FindJobNameSymbolBySprite(ReadableClientIdentityFile file, string spriteName)
    {
        var escapedSprite = Regex.Escape(spriteName);
        var match = Regex.Match(
            file.Content,
            $@"(?:JobNameTable\s*\[\s*jobtbl\.|\[\s*jobtbl\.)(?<symbol>JT_[A-Za-z0-9_]+)\s*\]\s*=\s*""{escapedSprite}""",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["symbol"].Value;
        }

        if (file.Format == NpcClientFileFormat.LegacyTxt)
        {
            foreach (var line in file.Content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = ParseLegacyTxtEntry(line);
                if (entry is { } parsed
                    && parsed.Value.Equals(spriteName, StringComparison.OrdinalIgnoreCase))
                {
                    return parsed.Key;
                }
            }
        }

        return null;
    }

    private static string? FindJobNameSpriteBySymbol(ReadableClientIdentityFile file, string symbol)
    {
        var escapedSymbol = Regex.Escape(symbol);
        var match = Regex.Match(
            file.Content,
            $@"(?:JobNameTable\s*\[\s*jobtbl\.{escapedSymbol}\s*\]|\[\s*jobtbl\.{escapedSymbol}\s*\])\s*=\s*""(?<sprite>[^""]+)""",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["sprite"].Value;
        }

        if (file.Format == NpcClientFileFormat.LegacyTxt)
        {
            foreach (var line in file.Content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = ParseLegacyTxtEntry(line);
                if (entry is { } parsed
                    && parsed.Key.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                {
                    return parsed.Value;
                }
            }
        }

        return null;
    }

    private static int? FindIdentityValueBySymbol(ReadableClientIdentityFile file, string symbol)
    {
        var escapedSymbol = Regex.Escape(symbol);
        var match = Regex.Match(
            file.Content,
            $@"(?:\b(?:JTtbl|jobtbl)\.{escapedSymbol}\b|\b{escapedSymbol}\b)\s*=\s*(?<value>\d+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["value"].Value, out var parsed))
        {
            return parsed;
        }

        if (file.Format == NpcClientFileFormat.LegacyTxt)
        {
            foreach (var line in file.Content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = ParseLegacyTxtEntry(line);
                if (entry is { } parsedEntry
                    && parsedEntry.Key.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(parsedEntry.Value, out parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static string? FindSymbolByIdentityValue(ReadableClientIdentityFile file, int identityId)
    {
        var match = Regex.Match(
            file.Content,
            $@"(?<symbol>JT_[A-Za-z0-9_]+)\s*=\s*{identityId}\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["symbol"].Value;
        }

        if (file.Format == NpcClientFileFormat.LegacyTxt)
        {
            foreach (var line in file.Content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = ParseLegacyTxtEntry(line);
                if (entry is { } parsedEntry
                    && parsedEntry.Value.Equals(identityId.ToString(), StringComparison.Ordinal))
                {
                    return parsedEntry.Key;
                }
            }
        }

        return null;
    }

    private string? TryReadLuaText(string path, List<string> warnings)
    {
        var inspection = _formatDetector.Inspect(path);
        if (inspection.Format == LuaScriptFormat.Missing)
        {
            warnings.Add($"{Path.GetFileName(path)} was not found for NPC sprite validation.");
            return null;
        }

        if (!inspection.ReadableAsText)
        {
            warnings.Add($"{Path.GetFileName(path)} is not readable as plain text in this milestone; NPC sprite validation is partial.");
            return null;
        }

        return File.ReadAllText(path);
    }

    private static IReadOnlyList<string> FindLooseSpriteCandidates(string patchPath, string sprite)
    {
        var dataPath = SafeFileSystem.Combine(patchPath, "data");
        if (!Directory.Exists(dataPath))
        {
            return [];
        }

        var matches = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (!SpriteAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Path.GetFileNameWithoutExtension(file).Equals(sprite, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(file);
            }
        }

        return matches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private GrfAssetLookupResult? ResolveSpriteAssetLookup(RepositoryPaths paths, string sprite)
    {
        if (_grfAssetLookupService is null || !_grfAssetLookupOptions.Enabled)
        {
            return null;
        }

        return _grfAssetLookupService.FindAssets(
            paths,
            sprite,
            SpriteAssetExtensions,
            _grfAssetLookupOptions);
    }

    private static string BuildGrfDetectionSource(GrfAssetLookupResult lookup) =>
        lookup.Source switch
        {
            GrfAssetLookupSource.LocalIndex => "grf-custom-sprite/local-index",
            GrfAssetLookupSource.LiveScanFallback => "grf-custom-sprite/live-scan-fallback",
            GrfAssetLookupSource.LiveScan => "grf-custom-sprite/live-scan",
            _ => "grf-custom-sprite"
        };

    private static string? NormalizeClientSymbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim()
            .Replace('-', '_')
            .Replace(' ', '_')
            .ToUpperInvariant();
        if (!normalized.StartsWith("JT_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "JT_" + normalized;
        }

        return Regex.IsMatch(normalized, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
            ? normalized
            : null;
    }

    private static string BuildSuggestedSymbol(string sprite)
    {
        var normalized = new string(sprite
            .Trim()
            .ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
        normalized = Regex.Replace(normalized, @"_+", "_", RegexOptions.CultureInvariant).Trim('_');
        if (normalized.Length == 0)
        {
            normalized = "CUSTOM_NPC";
        }

        return normalized.StartsWith("JT_", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : "JT_" + normalized;
    }

    private static string FirstPreviewLine(string preview) =>
        preview.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0)
        ?? string.Empty;

    private static string? ProbeLegacyTxtStyleFromContent(string content)
    {
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Contains('='))
            {
                return "=";
            }

            if (trimmed.Contains(','))
            {
                return ",";
            }
        }

        return null;
    }

    private static LegacyTxtEntry? ParseLegacyTxtEntry(string rawLine)
    {
        var trimmed = rawLine.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return null;
        }

        var delimiter = trimmed.Contains('=') ? "=" : trimmed.Contains(',') ? "," : null;
        if (delimiter is null)
        {
            return null;
        }

        var parts = trimmed.Split([delimiter], 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            return null;
        }

        return new LegacyTxtEntry(parts[0].Trim(), parts[1].Trim());
    }

    private sealed record ReadableClientIdentityFile(
        string LogicalName,
        string Path,
        NpcClientFileFormat Format,
        string Content,
        string? LegacyStyle);

    private sealed record ExistingClientIdentityState(
        bool CompleteRegistration,
        bool HasConflicts,
        IReadOnlyList<string> Details,
        IReadOnlyList<string> Errors);

    private sealed record LegacyTxtEntry(
        string Key,
        string Value);
}

internal sealed record NpcClientIdentityPlanningResult(
    NpcSpriteValidation SpriteValidation,
    NpcSpriteResolution SpriteResolution,
    NpcClientIdentityPlan Plan,
    IReadOnlyList<ItemDependency> Dependencies,
    IReadOnlyList<ProposedFileChange> ProposedChanges);
