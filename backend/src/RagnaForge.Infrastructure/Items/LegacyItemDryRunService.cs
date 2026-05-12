using System.Text;
using System.Text.RegularExpressions;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Discovery;
using RagnaForge.Domain.Items;
using RagnaForge.Infrastructure.FileSystem;
using RagnaForge.Infrastructure.Patch;
using RagnaForge.Infrastructure.Rathena;

namespace RagnaForge.Infrastructure.Items;

public sealed partial class LegacyItemDryRunService
{
    private const int DefaultStartId = 50000;
    private static readonly string[] ItemDatabaseFiles =
    [
        "db/pre-re/item_db.yml",
        "db/re/item_db.yml",
        "db/import/item_db.yml"
    ];

    private static readonly string[] CandidateAssetExtensions =
    [
        ".bmp",
        ".png",
        ".tga",
        ".spr",
        ".act"
    ];

    private readonly IGrfAssetLookupService? _grfAssetLookupService;
    private readonly GrfAssetLookupOptions _grfAssetLookupOptions;
    private readonly ClientSidePlanner _clientSidePlanner = new();

    public LegacyItemDryRunService()
        : this(null, GrfAssetLookupOptions.Disabled)
    {
    }

    public LegacyItemDryRunService(
        IGrfAssetLookupService? grfAssetLookupService,
        GrfAssetLookupOptions grfAssetLookupOptions)
    {
        _grfAssetLookupService = grfAssetLookupService;
        _grfAssetLookupOptions = grfAssetLookupOptions;
    }

    public ItemDryRunReport Create(
        RepositoryPaths paths,
        EpisodeProfile episodeProfile,
        ItemDefinitionInput input)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(episodeProfile);
        ArgumentNullException.ThrowIfNull(input);

        var warnings = new List<string>();
        var dependencies = new List<ItemDependency>();
        var proposedChanges = new List<ProposedFileChange>();
        GrfAssetLookupResult? assetLookup = null;

        var rathena = new RathenaScanner().Scan(paths.RathenaPath);
        var patch = new PatchScanner().Scan(paths.PatchPath);
        var existingItems = ReadExistingItems(paths.RathenaPath);
        var patchIds = ReadPatchIds(paths.PatchPath);
        var serverTarget = Path.Combine(paths.RathenaPath, "db", "import", "item_db.yml");

        var resolvedId = input.Id ?? AllocateId(existingItems.Ids, patchIds, DefaultStartId);
        if (input.Id is null)
        {
            warnings.Add($"ID was not provided; suggested free ID {resolvedId} will be used in the dry-run.");
        }

        var normalizedResource = string.IsNullOrWhiteSpace(input.ResourceName) ? input.AegisName : input.ResourceName;
        if (string.IsNullOrWhiteSpace(input.ResourceName))
        {
            warnings.Add("Resource name was not provided; dry-run is using AegisName as the client resource name.");
        }

        var identifiedDesc = NormalizeDescription(input.IdentifiedDescriptionLines, input.DisplayName, warnings, "identified");
        var unidentifiedName = string.IsNullOrWhiteSpace(input.UnidentifiedDisplayName) ? input.DisplayName : input.UnidentifiedDisplayName!;
        var unidentifiedResource = string.IsNullOrWhiteSpace(input.UnidentifiedResourceName) ? normalizedResource : input.UnidentifiedResourceName!;
        var unidentifiedDesc = NormalizeDescription(input.UnidentifiedDescriptionLines, input.DisplayName, warnings, "unidentified");

        var clientDateUsed = !string.IsNullOrWhiteSpace(episodeProfile.ClientDate)
            ? episodeProfile.ClientDate
            : patch.ClientDate.Value;
        var clientDateSource = !string.IsNullOrWhiteSpace(episodeProfile.ClientDate)
            ? "episode-profile"
            : patch.ClientDate.Source;

        if (!File.Exists(serverTarget))
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Missing, "Target import item DB was not found.", serverTarget));
        }
        else
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Satisfied, "Target import item DB is available.", serverTarget));
        }

        if (existingItems.Ids.Contains(resolvedId))
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Missing, $"Item ID {resolvedId} already exists in the active item databases."));
        }
        else
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Satisfied, $"Item ID {resolvedId} is currently free."));
        }

        if (existingItems.AegisNames.Contains(input.AegisName))
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Missing, $"AegisName '{input.AegisName}' already exists in the active item databases."));
        }
        else
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Satisfied, $"AegisName '{input.AegisName}' is currently free."));
        }

        var normalizedInput = input with
        {
            ResourceName = normalizedResource,
            IdentifiedDescriptionLines = identifiedDesc,
            UnidentifiedDisplayName = unidentifiedName,
            UnidentifiedResourceName = unidentifiedResource,
            UnidentifiedDescriptionLines = unidentifiedDesc
        };

        var clientSidePlan = _clientSidePlanner.CreateItemPlan(
            paths.PatchPath,
            resolvedId,
            normalizedInput,
            identifiedDesc,
            unidentifiedName,
            unidentifiedResource,
            unidentifiedDesc);

        dependencies.Add(new ItemDependency(
            "Patch",
            clientSidePlan.CanApply ? ItemDependencyState.Satisfied : ItemDependencyState.Missing,
            $"Client-side item plan mode is {clientSidePlan.ClientSideMode}; {clientSidePlan.ProposedRegistrations.Count} registration proposal(s)."));

        foreach (var block in clientSidePlan.BlockReasons)
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Missing, block));
        }

        foreach (var warning in clientSidePlan.ValidationWarnings)
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Warning, warning));
        }

        if (patchIds.Contains(resolvedId))
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Warning, $"Client-side tables already contain ID {resolvedId}; review overrides before applying."));
        }

        if (!string.IsNullOrWhiteSpace(clientDateUsed))
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Satisfied, $"Client date resolved as {clientDateUsed} from {clientDateSource}."));
        }
        else
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Warning, "Client date could not be confirmed; client-side dependency resolution remains profile-sensitive."));
        }

        var assetCandidates = FindLooseAssetCandidates(paths.PatchPath, normalizedResource);
        if (assetCandidates.Count > 0)
        {
            dependencies.Add(new ItemDependency("Assets", ItemDependencyState.Satisfied, $"Found {assetCandidates.Count} loose asset candidate(s) for resource '{normalizedResource}'.", assetCandidates[0]));
        }
        else
        {
            assetLookup = _grfAssetLookupService?.FindAssets(
                paths,
                normalizedResource,
                CandidateAssetExtensions,
                _grfAssetLookupOptions);
            foreach (var lookupWarning in assetLookup?.Warnings ?? [])
            {
                warnings.Add(lookupWarning);
            }

            if (assetLookup is { Matches.Count: > 0 })
            {
                var firstMatch = assetLookup.Matches[0];
                dependencies.Add(new ItemDependency(
                    "Assets",
                    ItemDependencyState.Satisfied,
                    $"Found {assetLookup.Matches.Count} GRF asset candidate(s) for resource '{normalizedResource}' after scanning {assetLookup.ContainersScanned} container(s) {DescribeLookupSource(assetLookup)}.",
                    $"{firstMatch.ContainerPath}::{firstMatch.RelativePath}"));
            }
            else if (assetLookup is { Searched: true })
            {
                dependencies.Add(new ItemDependency(
                    "Assets",
                    ItemDependencyState.Warning,
                    $"No loose patch or GRF asset matched resource '{normalizedResource}' after scanning {assetLookup.ContainersScanned} container(s) {DescribeLookupSource(assetLookup)}."));
            }
            else
            {
                dependencies.Add(new ItemDependency("Assets", ItemDependencyState.Warning, $"No loose patch asset matched resource '{normalizedResource}'. Enable GRF asset lookup to inspect container contents."));
            }
        }

        if (!rathena.HasDbImport)
        {
            dependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Missing, "db/import was not detected in the rAthena repository."));
        }

        if (LooksLikeEquipment(input.Type, input.Slots))
        {
            warnings.Add("This dry-run is item-first. Equipment-specific visual/datainfo validation is not included yet.");
        }

        proposedChanges.Add(new ProposedFileChange(
            serverTarget,
            "append",
            File.Exists(serverTarget),
            BuildItemDbSnippet(resolvedId, normalizedInput)));

        foreach (var change in clientSidePlan.ProposedChanges)
        {
            proposedChanges.Add(change);
        }

        dependencies.Add(new ItemDependency("DryRun", ItemDependencyState.Proposed, $"Prepared {proposedChanges.Count} proposed file change(s) without touching external repositories."));

        var canApply = dependencies.All(item => item.State != ItemDependencyState.Missing);
        var diffPreview = ItemDiffPreviewBuilder.Build(proposedChanges);

        return new ItemDryRunReport(
            DateTimeOffset.UtcNow,
            episodeProfile,
            clientDateUsed,
            clientDateSource,
            patch.ItemDataMode.UsesLegacyTables ? "legacy-tables" : "unsupported",
            input,
            resolvedId,
            canApply,
            dependencies,
            proposedChanges,
            diffPreview,
            warnings,
            assetLookup,
            clientSidePlan);
    }

    private static string DescribeLookupSource(GrfAssetLookupResult lookup) =>
        lookup.Source switch
        {
            GrfAssetLookupSource.LocalIndex => lookup.LocalIndexesLoaded > 0
                ? $"via local index ({lookup.LocalIndexesLoaded} loaded)"
                : "via local index",
            GrfAssetLookupSource.LiveScanFallback => lookup.LocalIndexesLoaded > 0
                ? $"via live scan fallback after checking {lookup.LocalIndexesLoaded} local index(es)"
                : "via live scan fallback",
            GrfAssetLookupSource.LiveScan => "via live scan",
            _ => "during GRF lookup"
        };

    private static (HashSet<int> Ids, HashSet<string> AegisNames) ReadExistingItems(string rathenaPath)
    {
        var ids = new HashSet<int>();
        var aegisNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in ItemDatabaseFiles)
        {
            var fullPath = SafeFileSystem.Combine(rathenaPath, relativePath.Split('/'));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(fullPath))
            {
                var idMatch = ItemIdRegex().Match(line);
                if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var id))
                {
                    ids.Add(id);
                }

                var aegisMatch = AegisNameRegex().Match(line);
                if (aegisMatch.Success)
                {
                    aegisNames.Add(aegisMatch.Groups[1].Value.Trim());
                }
            }
        }

        return (ids, aegisNames);
    }

    private static HashSet<int> ReadPatchIds(string patchPath)
    {
        var ids = new HashSet<int>();
        var dataPath = SafeFileSystem.Combine(patchPath, "data");
        var files = new[]
        {
            "idnum2itemdisplaynametable.txt",
            "idnum2itemresnametable.txt",
            "num2itemdisplaynametable.txt",
            "num2itemresnametable.txt",
            "itemslotcounttable.txt"
        };

        foreach (var file in files)
        {
            var fullPath = SafeFileSystem.Combine(dataPath, file);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(fullPath))
            {
                var match = LegacyTableLineRegex().Match(line);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    private static int AllocateId(HashSet<int> existingIds, HashSet<int> patchIds, int startAt)
    {
        var current = Math.Max(1, startAt);
        while (existingIds.Contains(current) || patchIds.Contains(current))
        {
            current++;
        }

        return current;
    }

    private static IReadOnlyList<string> NormalizeDescription(
        IReadOnlyList<string> descriptionLines,
        string fallbackDisplayName,
        List<string> warnings,
        string label)
    {
        var lines = descriptionLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();

        if (lines.Length > 0)
        {
            return lines;
        }

        warnings.Add($"No {label} description was provided; dry-run is using the display name as a placeholder description.");
        return [$"{fallbackDisplayName}."];
    }

    private static bool LooksLikeEquipment(string itemType, int slots) =>
        slots > 0
        || itemType.Contains("weapon", StringComparison.OrdinalIgnoreCase)
        || itemType.Contains("armor", StringComparison.OrdinalIgnoreCase)
        || itemType.Contains("shadow", StringComparison.OrdinalIgnoreCase)
        || itemType.Contains("ammo", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> FindLooseAssetCandidates(string patchPath, string resourceName)
    {
        var results = new List<string>();
        foreach (var root in new[]
                 {
                     SafeFileSystem.Combine(patchPath, "data"),
                     SafeFileSystem.Combine(patchPath, "texture"),
                     SafeFileSystem.Combine(patchPath, "system")
                 })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in SafeFileSystem.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!CandidateAssetExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Path.GetFileNameWithoutExtension(file.Name).Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(file.FullName);
                    if (results.Count >= 10)
                    {
                        return results;
                    }
                }
            }
        }

        return results;
    }

    private static string BuildItemDbSnippet(int resolvedId, ItemDefinitionInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("# RagnaForge dry-run proposal");
        builder.AppendLine($"  - Id: {resolvedId}");
        builder.AppendLine($"    AegisName: {input.AegisName}");
        builder.AppendLine($"    Name: {input.DisplayName}");
        builder.AppendLine($"    Type: {input.Type}");
        builder.AppendLine($"    Buy: {input.Buy}");
        builder.AppendLine($"    Sell: {input.Sell}");
        builder.AppendLine($"    Weight: {input.Weight}");

        if (input.Slots > 0)
        {
            builder.AppendLine($"    Slots: {input.Slots}");
        }

        if (!string.IsNullOrWhiteSpace(input.Script))
        {
            builder.AppendLine("    Script: |");
            foreach (var line in input.Script.Replace("\\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                builder.AppendLine($"      {line.TrimEnd()}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<ProposedFileChange> BuildLegacyClientChanges(
        string patchPath,
        int id,
        string displayName,
        string resourceName,
        IReadOnlyList<string> identifiedDescriptionLines,
        string unidentifiedName,
        string unidentifiedResourceName,
        IReadOnlyList<string> unidentifiedDescriptionLines,
        int slots)
    {
        var dataPath = SafeFileSystem.Combine(patchPath, "data");

        yield return new ProposedFileChange(
            SafeFileSystem.Combine(dataPath, "idnum2itemdisplaynametable.txt"),
            "append",
            File.Exists(SafeFileSystem.Combine(dataPath, "idnum2itemdisplaynametable.txt")),
            $"{id}#{displayName}#");

        yield return new ProposedFileChange(
            SafeFileSystem.Combine(dataPath, "idnum2itemresnametable.txt"),
            "append",
            File.Exists(SafeFileSystem.Combine(dataPath, "idnum2itemresnametable.txt")),
            $"{id}#{resourceName}#");

        yield return new ProposedFileChange(
            SafeFileSystem.Combine(dataPath, "idnum2itemdesctable.txt"),
            "append",
            File.Exists(SafeFileSystem.Combine(dataPath, "idnum2itemdesctable.txt")),
            BuildDescriptionTableEntry(id, identifiedDescriptionLines));

        yield return new ProposedFileChange(
            SafeFileSystem.Combine(dataPath, "num2itemdisplaynametable.txt"),
            "append",
            File.Exists(SafeFileSystem.Combine(dataPath, "num2itemdisplaynametable.txt")),
            $"{id}#{unidentifiedName}#");

        yield return new ProposedFileChange(
            SafeFileSystem.Combine(dataPath, "num2itemresnametable.txt"),
            "append",
            File.Exists(SafeFileSystem.Combine(dataPath, "num2itemresnametable.txt")),
            $"{id}#{unidentifiedResourceName}#");

        yield return new ProposedFileChange(
            SafeFileSystem.Combine(dataPath, "num2itemdesctable.txt"),
            "append",
            File.Exists(SafeFileSystem.Combine(dataPath, "num2itemdesctable.txt")),
            BuildDescriptionTableEntry(id, unidentifiedDescriptionLines));

        if (slots > 0)
        {
            yield return new ProposedFileChange(
                SafeFileSystem.Combine(dataPath, "itemslotcounttable.txt"),
                "append",
                File.Exists(SafeFileSystem.Combine(dataPath, "itemslotcounttable.txt")),
                $"{id}#{slots}#");
        }
    }

    private static string BuildDescriptionTableEntry(int id, IReadOnlyList<string> descriptionLines)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{id}#");
        foreach (var line in descriptionLines)
        {
            builder.AppendLine(line);
        }

        builder.Append("#");
        return builder.ToString();
    }

    [GeneratedRegex(@"^\s*-\s+Id\s*:\s*(\d+)\s*$", RegexOptions.Compiled)]
    private static partial Regex ItemIdRegex();

    [GeneratedRegex(@"^\s*AegisName\s*:\s*(.+?)\s*$", RegexOptions.Compiled)]
    private static partial Regex AegisNameRegex();

    [GeneratedRegex(@"^\s*(\d+)\s*#", RegexOptions.Compiled)]
    private static partial Regex LegacyTableLineRegex();
}
