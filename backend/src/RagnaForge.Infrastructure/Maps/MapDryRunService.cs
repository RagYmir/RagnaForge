using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Maps;
using RagnaForge.Infrastructure.FileSystem;
using RagnaForge.Infrastructure.Items;

namespace RagnaForge.Infrastructure.Maps;

public sealed class MapDryRunService(
    IGrfAssetLookupService? grfAssetLookupService = null,
    GrfAssetLookupOptions? grfAssetLookupOptions = null,
    IGrfFileExtractor? grfFileExtractor = null,
    string? temporaryExtractionRoot = null)
{
    private const long MaxTemporaryMapAssetBytes = 64L * 1024L * 1024L;

    private readonly IGrfAssetLookupService? _grfAssetLookupService = grfAssetLookupService;
    private readonly IGrfFileExtractor? _grfFileExtractor = grfFileExtractor;
    private readonly string? _temporaryExtractionRoot = temporaryExtractionRoot;
    private readonly GrfAssetLookupOptions _grfAssetLookupOptions = grfAssetLookupService is null
        ? GrfAssetLookupOptions.Disabled
        : grfAssetLookupOptions ?? GrfAssetLookupOptions.Disabled;

    public MapDryRunReport Create(RepositoryPaths paths, EpisodeProfile episodeProfile, MapDeploymentInput input)
    {
        var dependencies = new List<ItemDependency>();
        var proposedChanges = new List<ProposedFileChange>();
        var warnings = new List<string>();

        var mapIndexPath = SafeFileSystem.Combine(paths.RathenaPath, "db", "import", "map_index.txt");
        var mapsAthenaPath = SafeFileSystem.Combine(paths.RathenaPath, "conf", "maps_athena.conf");
        var mapCachePath = SafeFileSystem.Combine(paths.RathenaPath, "db", "import", "map_cache.dat");
        var mapCacheToolPath = SafeFileSystem.Combine(paths.RathenaPath, "mapcache.exe");
        var patchDataPath = SafeFileSystem.Combine(paths.PatchPath, "data");

        dependencies.Add(File.Exists(mapIndexPath)
            ? new ItemDependency("rAthena", ItemDependencyState.Satisfied, "db/import/map_index.txt is available.", mapIndexPath)
            : new ItemDependency("rAthena", ItemDependencyState.Missing, "db/import/map_index.txt was not found.", mapIndexPath));
        dependencies.Add(File.Exists(mapsAthenaPath)
            ? new ItemDependency("rAthena", ItemDependencyState.Satisfied, "conf/maps_athena.conf is available.", mapsAthenaPath)
            : new ItemDependency("rAthena", ItemDependencyState.Missing, "conf/maps_athena.conf was not found.", mapsAthenaPath));

        if (MapExists(paths.RathenaPath, input.MapName))
        {
            dependencies.Add(new ItemDependency("Map", ItemDependencyState.Missing, $"Map '{input.MapName}' is already registered in rAthena."));
        }
        else
        {
            dependencies.Add(new ItemDependency("Map", ItemDependencyState.Satisfied, $"Map '{input.MapName}' is currently free in rAthena registration."));
        }

        var rswResource = input.RswResourceName ?? input.MapName;
        var gndResource = input.GndResourceName ?? input.MapName;
        var gatResource = input.GatResourceName ?? input.MapName;
        var rswLookup = ResolveMapAsset(paths, rswResource, ".rsw");
        var gndLookup = ResolveMapAsset(paths, gndResource, ".gnd");
        var gatLookup = ResolveMapAsset(paths, gatResource, ".gat");
        var requiresBinaryRename =
            !input.MapName.Equals(rswResource, StringComparison.OrdinalIgnoreCase)
            || !input.MapName.Equals(gndResource, StringComparison.OrdinalIgnoreCase)
            || !input.MapName.Equals(gatResource, StringComparison.OrdinalIgnoreCase);

        dependencies.Add(BuildMapAssetDependency(paths.PatchPath, input.MapName, ".rsw", rswLookup));
        dependencies.Add(BuildMapAssetDependency(paths.PatchPath, input.MapName, ".gnd", gndLookup));
        dependencies.Add(BuildMapAssetDependency(paths.PatchPath, input.MapName, ".gat", gatLookup));

        if (requiresBinaryRename)
        {
            dependencies.Add(new ItemDependency(
                "Map",
                ItemDependencyState.Missing,
                $"Binary map rename is not supported in this phase. Keep MapName '{input.MapName}' aligned with .rsw/.gnd/.gat resource names before apply."));
        }

        var looseRswPath = SafeFileSystem.Combine(patchDataPath, input.MapName + ".rsw");
        var looseGndPath = SafeFileSystem.Combine(patchDataPath, input.MapName + ".gnd");
        var dependencyScan = BuildDependencyScan(paths, looseRswPath, looseGndPath, rswLookup, gndLookup, warnings);
        if (dependencyScan is not null)
        {
            warnings.AddRange(dependencyScan.Warnings);
            foreach (var dependency in BuildDependencyScanSummaries(dependencyScan))
            {
                dependencies.Add(dependency);
            }

            foreach (var ambiguousAsset in dependencyScan.ReferencedAssets
                         .Where(asset => !asset.ExistsInLoosePatch && asset.Lookup is { Matches.Count: > 1 }))
            {
                var firstMatch = ambiguousAsset.Lookup!.Matches[0];
                dependencies.Add(new ItemDependency(
                    "MapAssets",
                    ItemDependencyState.Missing,
                    $"Ambiguous GRF match for '{ambiguousAsset.ReferencePath}'. Map apply requires a unique asset source before copying.",
                    $"{firstMatch.ContainerPath}::{firstMatch.RelativePath}"));
            }
        }

        var assetPlans = BuildAssetPlans(paths, input, rswLookup, gndLookup, gatLookup, dependencyScan);

        if (File.Exists(mapCacheToolPath))
        {
            dependencies.Add(new ItemDependency(
                "MapCache",
                ItemDependencyState.Satisfied,
                "mapcache.exe is available for controlled map_cache.dat rebuild.",
                mapCacheToolPath));
        }
        else
        {
            dependencies.Add(new ItemDependency(
                "MapCache",
                ItemDependencyState.Missing,
                "mapcache.exe was not found in the rAthena root; map apply cannot rebuild map_cache.dat safely.",
                mapCacheToolPath));
        }

        if (File.Exists(mapCachePath))
        {
            dependencies.Add(new ItemDependency("Map", ItemDependencyState.Warning, "map_cache.dat exists, but will require rebuild after any real map apply.", mapCachePath));
        }
        else
        {
            dependencies.Add(new ItemDependency("Map", ItemDependencyState.Warning, "map_cache.dat was not found in db/import; deployment will require an explicit cache generation step.", mapCachePath));
        }

        proposedChanges.Add(new ProposedFileChange(
            mapIndexPath,
            "append",
            File.Exists(mapIndexPath),
            $"{input.MapName} 0"));

        proposedChanges.Add(new ProposedFileChange(
            mapsAthenaPath,
            "append",
            File.Exists(mapsAthenaPath),
            $"map: {input.MapName}"));

        dependencies.Add(new ItemDependency("DryRun", ItemDependencyState.Proposed, $"Prepared {proposedChanges.Count} text change(s) and {assetPlans.Count(plan => plan.NeedsCopy)} asset copy action(s) for map deployment preview."));
        var canApply = dependencies.All(item => item.State != ItemDependencyState.Missing);
        var diffPreview = ItemDiffPreviewBuilder.Build(proposedChanges);
        var mapCachePlan = BuildMapCachePlan(mapCacheToolPath, mapCachePath);
        return new MapDryRunReport(
            DateTimeOffset.UtcNow,
            episodeProfile,
            input,
            canApply,
            dependencies,
            proposedChanges,
            diffPreview,
            warnings,
            rswLookup,
            gndLookup,
            gatLookup,
            dependencyScan,
            assetPlans,
            mapCachePlan);
    }

    private MapDependencyScanResult? BuildDependencyScan(
        RepositoryPaths paths,
        string looseRswPath,
        string looseGndPath,
        GrfAssetLookupResult? rswLookup,
        GrfAssetLookupResult? gndLookup,
        List<string> warnings)
    {
        var scan = new MapReferencedAssetScanner().Scan(looseRswPath, looseGndPath);
        if (!scan.DeepScanAvailable && scan.ReferencedAssets.Count == 0)
        {
            var extractedScan = TryBuildDependencyScanFromGrf(paths, looseRswPath, looseGndPath, rswLookup, gndLookup, warnings);
            return extractedScan ?? scan;
        }

        return ResolveMapReferences(paths, scan);
    }

    private MapDependencyScanResult? TryBuildDependencyScanFromGrf(
        RepositoryPaths paths,
        string looseRswPath,
        string looseGndPath,
        GrfAssetLookupResult? rswLookup,
        GrfAssetLookupResult? gndLookup,
        List<string> warnings)
    {
        if (_grfFileExtractor is null || string.IsNullOrWhiteSpace(_temporaryExtractionRoot))
        {
            return null;
        }

        var matches = new List<GrfAssetLookupMatch>();
        if (!File.Exists(looseRswPath) && rswLookup is { Matches.Count: > 0 })
        {
            matches.Add(rswLookup.Matches[0]);
        }

        if (!File.Exists(looseGndPath) && gndLookup is { Matches.Count: > 0 })
        {
            matches.Add(gndLookup.Matches[0]);
        }

        if (matches.Count == 0)
        {
            return null;
        }

        var operationRoot = Path.Combine(_temporaryExtractionRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
        GrfFileExtractionResult extraction;
        try
        {
            extraction = _grfFileExtractor.ExtractFiles(paths, matches, operationRoot, MaxTemporaryMapAssetBytes);
        }
        catch (Exception ex)
        {
            warnings.Add($"Controlled GRF extraction for map dependency scan failed: {ex.Message}");
            return null;
        }

        warnings.AddRange(extraction.Warnings);
        if (extraction.Files.Count == 0)
        {
            TryDeleteDirectory(operationRoot);
            return null;
        }

        try
        {
            var extractedRsw = File.Exists(looseRswPath)
                ? looseRswPath
                : extraction.Files.FirstOrDefault(file => file.RelativePath.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase))?.ExtractedPath;
            var extractedGnd = File.Exists(looseGndPath)
                ? looseGndPath
                : extraction.Files.FirstOrDefault(file => file.RelativePath.EndsWith(".gnd", StringComparison.OrdinalIgnoreCase))?.ExtractedPath;

            var extractedScan = new MapReferencedAssetScanner().Scan(extractedRsw, extractedGnd);
            if (!extractedScan.DeepScanAvailable)
            {
                return null;
            }

            warnings.Add($"Deep map dependency scan used controlled temporary extraction for {extraction.Files.Count} GRF file(s); temporary files were cleaned after scanning.");
            return ResolveMapReferences(paths, extractedScan with
            {
                Source = "ControlledGrfExtraction"
            });
        }
        finally
        {
            TryDeleteDirectory(operationRoot);
        }
    }

    private MapDependencyScanResult ResolveMapReferences(RepositoryPaths paths, MapDependencyScanResult scan)
    {
        var resolvedAssets = new List<MapReferencedAsset>(scan.ReferencedAssets.Count);
        foreach (var asset in scan.ReferencedAssets)
        {
            var loosePath = SafeFileSystem.Combine(paths.PatchPath, "data", asset.ReferencePath);
            if (File.Exists(loosePath))
            {
                resolvedAssets.Add(asset with
                {
                    ExistsInLoosePatch = true,
                    LoosePath = loosePath,
                    Resolved = true
                });
                continue;
            }

            var lookup = ResolveReferencedAsset(paths, asset.ReferencePath);
            resolvedAssets.Add(asset with
            {
                ExistsInLoosePatch = false,
                LoosePath = loosePath,
                Lookup = lookup,
                Resolved = lookup is { Matches.Count: 1 }
            });
        }

        return scan with
        {
            ReferencedAssets = resolvedAssets
        };
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

    private IEnumerable<ItemDependency> BuildDependencyScanSummaries(MapDependencyScanResult dependencyScan)
    {
        if (!dependencyScan.DeepScanAvailable)
        {
            yield return new ItemDependency(
                "MapAssets",
                ItemDependencyState.Warning,
                "Deep map dependency scan could not inspect loose .rsw/.gnd files; textures, models, sounds and effects may still need manual confirmation.");
            yield break;
        }

        foreach (var categoryGroup in dependencyScan.ReferencedAssets
                     .GroupBy(asset => asset.Category, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var total = categoryGroup.Count();
            var resolved = categoryGroup.Count(asset => asset.Resolved);
            var missing = total - resolved;
            var resolvedLoose = categoryGroup.FirstOrDefault(asset => asset.Resolved && !string.IsNullOrWhiteSpace(asset.LoosePath));
            var resolvedLookup = categoryGroup.FirstOrDefault(asset => asset.Lookup is { Matches.Count: > 0 });
            var sourcePath = resolvedLoose?.LoosePath
                             ?? (resolvedLookup?.Lookup?.Matches.Count > 0
                                 ? $"{resolvedLookup.Lookup.Matches[0].ContainerPath}::{resolvedLookup.Lookup.Matches[0].RelativePath}"
                                 : null);

            var sampleMissing = categoryGroup
                .Where(asset => !asset.Resolved)
                .Take(3)
                .Select(asset => asset.ReferencePath)
                .ToArray();

            if (missing == 0)
            {
                yield return new ItemDependency(
                    "Map" + categoryGroup.Key,
                    ItemDependencyState.Satisfied,
                    $"{categoryGroup.Key} references resolved: {resolved}/{total}.",
                    sourcePath);
            }
            else
            {
                var sample = sampleMissing.Length > 0
                    ? " Missing examples: " + string.Join(", ", sampleMissing) + "."
                    : string.Empty;
                yield return new ItemDependency(
                    "Map" + categoryGroup.Key,
                    missing == total ? ItemDependencyState.Missing : ItemDependencyState.Warning,
                    $"{categoryGroup.Key} references resolved: {resolved}/{total}.{sample}",
                    sourcePath);
            }
        }
    }

    private ItemDependency BuildMapAssetDependency(string patchPath, string mapName, string extension, GrfAssetLookupResult? lookup)
    {
        var loosePath = SafeFileSystem.Combine(patchPath, "data", mapName + extension);
        if (File.Exists(loosePath))
        {
            return new ItemDependency("Patch", ItemDependencyState.Satisfied, $"Loose map asset '{mapName}{extension}' is present.", loosePath);
        }

        if (lookup is { Matches.Count: > 0 })
        {
            var firstMatch = lookup.Matches[0];
            return new ItemDependency("Assets", ItemDependencyState.Satisfied, $"Found GRF map asset '{mapName}{extension}' after scanning {lookup.ContainersScanned} container(s) {DescribeLookupSource(lookup)}.", $"{firstMatch.ContainerPath}::{firstMatch.RelativePath}");
        }

        if (lookup is { Searched: true })
        {
            return new ItemDependency("Assets", ItemDependencyState.Missing, $"Map asset '{mapName}{extension}' was not found in loose patch or GRF containers after scanning {lookup.ContainersScanned} container(s) {DescribeLookupSource(lookup)}.");
        }

        return new ItemDependency("Assets", ItemDependencyState.Missing, $"Map asset '{mapName}{extension}' was not found in loose patch files, and GRF map lookup is disabled.");
    }

    private static IReadOnlyList<MapAssetPlan> BuildAssetPlans(
        RepositoryPaths paths,
        MapDeploymentInput input,
        GrfAssetLookupResult? rswLookup,
        GrfAssetLookupResult? gndLookup,
        GrfAssetLookupResult? gatLookup,
        MapDependencyScanResult? dependencyScan)
    {
        var plans = new List<MapAssetPlan>
        {
            BuildCoreAssetPlan(paths, input.MapName, input.RswResourceName ?? input.MapName, ".rsw", "MapCore", rswLookup),
            BuildCoreAssetPlan(paths, input.MapName, input.GndResourceName ?? input.MapName, ".gnd", "MapCore", gndLookup),
            BuildCoreAssetPlan(paths, input.MapName, input.GatResourceName ?? input.MapName, ".gat", "MapCore", gatLookup)
        };

        if (dependencyScan is not null)
        {
            plans.AddRange(dependencyScan.ReferencedAssets
                .Select(asset => BuildReferencedAssetPlan(paths, asset))
                .OrderBy(plan => plan.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(plan => plan.RelativePath, StringComparer.OrdinalIgnoreCase));
        }

        return plans
            .GroupBy(plan => plan.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(plan => plan.ExistsInTarget)
                .ThenByDescending(plan => plan.NeedsCopy)
                .First())
            .OrderBy(plan => plan.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plan => plan.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static MapAssetPlan BuildCoreAssetPlan(
        RepositoryPaths paths,
        string mapName,
        string resourceName,
        string extension,
        string category,
        GrfAssetLookupResult? lookup)
    {
        var relativePath = mapName + extension;
        var targetPath = SafeFileSystem.Combine(paths.PatchPath, "data", relativePath);
        if (File.Exists(targetPath))
        {
            return new MapAssetPlan(
                category,
                relativePath,
                targetPath,
                true,
                "TargetLoosePatch",
                targetPath,
                null,
                true,
                false);
        }

        var resourcePath = SafeFileSystem.Combine(paths.PatchPath, "data", resourceName + extension);
        if (File.Exists(resourcePath))
        {
            return new MapAssetPlan(
                category,
                relativePath,
                targetPath,
                false,
                "LoosePatch",
                resourcePath,
                null,
                true,
                true);
        }

        if (lookup is { Matches.Count: > 0 })
        {
            var match = lookup.Matches[0];
            return new MapAssetPlan(
                category,
                relativePath,
                targetPath,
                false,
                "GrfExtraction",
                $"{match.ContainerPath}::{match.RelativePath}",
                match,
                true,
                true);
        }

        return new MapAssetPlan(
            category,
            relativePath,
            targetPath,
            false,
            "Missing",
            null,
            null,
            true,
            false);
    }

    private static MapAssetPlan BuildReferencedAssetPlan(
        RepositoryPaths paths,
        MapReferencedAsset asset)
    {
        var targetPath = SafeFileSystem.Combine(paths.PatchPath, "data", asset.ReferencePath);
        if (File.Exists(targetPath) || asset.ExistsInLoosePatch)
        {
            return new MapAssetPlan(
                asset.Category,
                asset.ReferencePath,
                targetPath,
                true,
                "TargetLoosePatch",
                targetPath,
                null,
                true,
                false);
        }

        if (!string.IsNullOrWhiteSpace(asset.LoosePath) && File.Exists(asset.LoosePath))
        {
            return new MapAssetPlan(
                asset.Category,
                asset.ReferencePath,
                targetPath,
                false,
                "LoosePatch",
                asset.LoosePath,
                null,
                true,
                true);
        }

        if (asset.Lookup is { Matches.Count: 1 })
        {
            var match = asset.Lookup.Matches[0];
            return new MapAssetPlan(
                asset.Category,
                asset.ReferencePath,
                targetPath,
                false,
                "GrfExtraction",
                $"{match.ContainerPath}::{match.RelativePath}",
                match,
                true,
                true);
        }

        if (asset.Lookup is { Matches.Count: > 1 })
        {
            return new MapAssetPlan(
                asset.Category,
                asset.ReferencePath,
                targetPath,
                false,
                "AmbiguousGrfLookup",
                null,
                null,
                true,
                false);
        }

        return new MapAssetPlan(
            asset.Category,
            asset.ReferencePath,
            targetPath,
            false,
            "Missing",
            null,
            null,
            true,
            false);
    }

    private static MapCachePlan BuildMapCachePlan(string mapCacheToolPath, string mapCachePath)
    {
        var toolDetected = File.Exists(mapCacheToolPath);
        var commands = toolDetected
            ? new[]
            {
                $"\"{mapCacheToolPath}\" -grf <generated-grf-list> -list <generated-map-list> -cache \"{mapCachePath}\""
            }
            : Array.Empty<string>();
        var notes = toolDetected
            ? new[]
            {
                "Map cache rebuild should run against a staging cache file before replacing the final target.",
                "Generated GRF list should include Patch/data as data_dir plus configured .grf containers."
            }
            : new[]
            {
                "mapcache.exe is required to rebuild map_cache.dat when use_grf is disabled."
            };

        return new MapCachePlan(
            toolDetected,
            toolDetected ? mapCacheToolPath : null,
            mapCachePath,
            File.Exists(mapCachePath),
            commands,
            notes);
    }

    private GrfAssetLookupResult? ResolveMapAsset(RepositoryPaths paths, string resourceName, string extension)
    {
        if (_grfAssetLookupService is null || !_grfAssetLookupOptions.Enabled)
        {
            return null;
        }

        return _grfAssetLookupService.FindAssets(
            paths,
            resourceName,
            [extension],
            _grfAssetLookupOptions);
    }

    private GrfAssetLookupResult? ResolveReferencedAsset(RepositoryPaths paths, string referencePath)
    {
        if (_grfAssetLookupService is null || !_grfAssetLookupOptions.Enabled)
        {
            return null;
        }

        var extension = Path.GetExtension(referencePath);
        var fileName = Path.GetFileNameWithoutExtension(referencePath);
        if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var directoryHints = referencePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return _grfAssetLookupService.FindAssets(
            paths,
            fileName,
            [extension],
            _grfAssetLookupOptions with
            {
                AllowContainsMatch = directoryHints.Length > 0,
                NameHints = directoryHints
            });
    }

    private static bool MapExists(string rathenaPath, string mapName)
    {
        var mapIndexPath = SafeFileSystem.Combine(rathenaPath, "db", "import", "map_index.txt");
        var mapsAthenaPath = SafeFileSystem.Combine(rathenaPath, "conf", "maps_athena.conf");

        return SafeFileSystem.ReadLinesIfExists(mapIndexPath).Any(line => line.TrimStart().StartsWith(mapName + " ", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals(mapName, StringComparison.OrdinalIgnoreCase))
               || SafeFileSystem.ReadLinesIfExists(mapsAthenaPath).Any(line => line.Trim().Equals($"map: {mapName}", StringComparison.OrdinalIgnoreCase));
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

    private static string NormalizeLookupToken(string value) =>
        new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
