using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Discovery;

namespace RagnaForge.Infrastructure.Grf;

public sealed class IndexedGrfAssetLookupService(
    IGrfContainerIndexStore indexStore,
    IGrfAssetLookupService? fallbackLookupService = null) : IGrfAssetLookupService
{
    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled || string.IsNullOrWhiteSpace(resourceName))
        {
            return fallbackLookupService?.FindAssets(paths, resourceName, extensions, options)
                   ?? new GrfAssetLookupResult(resourceName, false, 0, 0, [], [], GrfAssetLookupSource.None);
        }

        var containers = options.ContainerPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, options.MaxContainers))
            .ToArray();

        if (containers.Length == 0)
        {
            return fallbackLookupService?.FindAssets(paths, resourceName, extensions, options)
                   ?? new GrfAssetLookupResult(resourceName, false, 0, options.ContainerPaths.Count, [], ["GRF asset lookup was enabled, but no containers were provided."], GrfAssetLookupSource.None);
        }

        var normalizedExtensions = extensions
            .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedNameHints = (options.NameHints ?? [])
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var warnings = new List<string>();
        var matches = new List<GrfAssetLookupMatch>();
        var indexesLoaded = 0;
        var canTrustNegativeResult = true;

        foreach (var containerPath in containers)
        {
            var indexPath = indexStore.BuildDefaultIndexPath(containerPath);
            GrfContainerContentIndexDocument? document;
            try
            {
                document = indexStore.TryLoad(indexPath);
            }
            catch (Exception ex)
            {
                document = null;
                warnings.Add($"Failed to load GRF content index for {containerPath}: {ex.Message}");
            }

            if (document is null)
            {
                canTrustNegativeResult = false;
                continue;
            }

            indexesLoaded++;
            if (document.IsTruncated)
            {
                canTrustNegativeResult = false;
            }

            var containerMatches = new List<(GrfAssetLookupMatch Match, int Score)>();
            foreach (var entry in document.Entries)
            {
                if (!normalizedExtensions.Contains(entry.Extension))
                {
                    continue;
                }

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entry.FileName);
                var isExactMatch = fileNameWithoutExtension.Equals(resourceName, StringComparison.OrdinalIgnoreCase);
                var score = isExactMatch
                    ? 100
                    : ScoreContainsMatch(fileNameWithoutExtension, entry.RelativePath, normalizedNameHints, options.AllowContainsMatch);

                if (score <= 0)
                {
                    continue;
                }

                containerMatches.Add((new GrfAssetLookupMatch(
                    containerPath,
                    entry.RelativePath.Replace('\\', '/'),
                    entry.Extension,
                    entry.SizeCompressed,
                    entry.SizeDecompressed,
                    entry.Encrypted), score));
            }

            foreach (var match in containerMatches
                         .OrderByDescending(item => item.Score)
                         .ThenBy(item => item.Match.RelativePath, StringComparer.OrdinalIgnoreCase)
                         .Take(Math.Max(1, options.MaxMatchesPerContainer)))
            {
                matches.Add(match.Match);
            }

            if (containerMatches.Count > Math.Max(1, options.MaxMatchesPerContainer))
            {
                warnings.Add($"Indexed match capture for {containerPath} was limited to {Math.Max(1, options.MaxMatchesPerContainer)} result(s).");
            }
        }

        if (matches.Count > 0)
        {
            return new GrfAssetLookupResult(
                resourceName,
                true,
                indexesLoaded,
                options.ContainerPaths.Count,
                matches,
                warnings,
                GrfAssetLookupSource.LocalIndex,
                indexesLoaded,
                0);
        }

        if (fallbackLookupService is not null && (!canTrustNegativeResult || indexesLoaded == 0))
        {
            var fallbackResult = fallbackLookupService.FindAssets(paths, resourceName, extensions, options);
            return fallbackResult with
            {
                Warnings = warnings.Concat(fallbackResult.Warnings).ToArray(),
                Source = fallbackResult.Searched ? GrfAssetLookupSource.LiveScanFallback : fallbackResult.Source,
                LocalIndexesLoaded = indexesLoaded,
                LiveContainersScanned = fallbackResult.LiveContainersScanned > 0
                    ? fallbackResult.LiveContainersScanned
                    : fallbackResult.ContainersScanned
            };
        }

        return new GrfAssetLookupResult(
            resourceName,
            indexesLoaded > 0,
            indexesLoaded,
            options.ContainerPaths.Count,
            [],
            warnings,
            indexesLoaded > 0 ? GrfAssetLookupSource.LocalIndex : GrfAssetLookupSource.None,
            indexesLoaded,
            0);
    }

    private static int ScoreContainsMatch(
        string fileNameWithoutExtension,
        string relativePath,
        IReadOnlyList<string> normalizedNameHints,
        bool allowContainsMatch)
    {
        if (!allowContainsMatch || normalizedNameHints.Count == 0)
        {
            return 0;
        }

        var normalizedFileName = NormalizeLookupToken(fileNameWithoutExtension);
        var normalizedRelativePath = NormalizeLookupToken(relativePath);
        var score = 0;

        foreach (var token in normalizedNameHints)
        {
            if (normalizedFileName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
            else if (normalizedRelativePath.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        return score;
    }

    private static string NormalizeLookupToken(string value) =>
        new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
