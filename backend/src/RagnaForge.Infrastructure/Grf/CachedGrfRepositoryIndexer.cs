using RagnaForge.Application.Abstractions;
using RagnaForge.Application.Grf;
using RagnaForge.Domain.Discovery;
using RagnaForge.Infrastructure.FileSystem;

namespace RagnaForge.Infrastructure.Grf;

public sealed class CachedGrfRepositoryIndexer(IGrfRepositoryIndexStore indexStore)
{
    private static readonly HashSet<string> ContainerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".grf",
        ".gpf",
        ".thor",
        ".rgz",
        ".zip"
    };

    public GrfRepositoryIndexResult Build(
        GrfRepositoryIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        if (!SafeFileSystem.DirectoryExists(options.RootPath))
        {
            throw new DirectoryNotFoundException($"GRF repository path was not found: {options.RootPath}");
        }

        var warnings = new List<string>();
        var limit = Math.Max(1, options.MaxContainers);
        var previous = options.ForceRefresh ? null : indexStore.TryLoad(options.CachePath);
        var previousByPath = previous?.Containers.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, GrfContainerSnapshot>(StringComparer.OrdinalIgnoreCase);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var containers = new List<GrfContainerSnapshot>();
        var addedCount = 0;
        var changedCount = 0;
        var unchangedCount = 0;
        var limitReached = false;

        foreach (var file in SafeFileSystem.EnumerateFiles(options.RootPath, "*.*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ContainerExtensions.Contains(file.Extension))
            {
                continue;
            }

            if (containers.Count >= limit)
            {
                limitReached = true;
                break;
            }

            seenPaths.Add(file.FullName);
            var lastWriteTimeUtc = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            if (previousByPath.TryGetValue(file.FullName, out var cached)
                && cached.Length == file.Length
                && cached.LastWriteTimeUtc == lastWriteTimeUtc)
            {
                containers.Add(cached);
                unchangedCount++;
                continue;
            }

            containers.Add(new GrfContainerSnapshot(
                file.FullName,
                file.Name,
                file.Extension,
                file.Length,
                lastWriteTimeUtc));

            if (cached is null)
            {
                addedCount++;
            }
            else
            {
                changedCount++;
            }
        }

        if (limitReached)
        {
            warnings.Add($"Container index stopped at the configured limit of {limit} entries.");
        }

        var removedCount = previousByPath.Keys.Count(path => !seenPaths.Contains(path));
        var topLevelDirectoryCount = Directory.EnumerateDirectories(options.RootPath, "*", SearchOption.TopDirectoryOnly).Count();
        var orderedContainers = containers.OrderByDescending(container => container.Length).ToArray();
        var document = GrfRepositoryIndexDocument.Create(
            options.RootPath,
            topLevelDirectoryCount,
            orderedContainers);

        if (options.SaveCache)
        {
            indexStore.Save(options.CachePath, document, overwrite: true);
        }

        return new GrfRepositoryIndexResult(
            document,
            new GrfRepositoryIndexSummary(
                options.CachePath,
                previous is not null,
                options.SaveCache,
                limitReached,
                previous?.TotalContainerCount ?? 0,
                document.TotalContainerCount,
                addedCount,
                changedCount,
                unchangedCount,
                removedCount,
                orderedContainers.Length,
                warnings));
    }
}
