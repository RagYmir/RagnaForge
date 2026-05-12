using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;
using RagnaForge.Infrastructure.FileSystem;

namespace RagnaForge.Infrastructure.Grf;

public sealed class GrfRepositoryScanner : IGrfRepositoryScanner
{
    private static readonly HashSet<string> ContainerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".grf",
        ".gpf",
        ".thor",
        ".rgz",
        ".zip"
    };

    public GrfRepositoryDiscoveryResult Scan(string rootPath, int maxContainers)
    {
        if (!SafeFileSystem.DirectoryExists(rootPath))
        {
            return new GrfRepositoryDiscoveryResult(
                rootPath,
                false,
                0,
                0,
                false,
                [],
                ["GRF repository path was not found."]);
        }

        var warnings = new List<string>();
        var limit = Math.Max(1, maxContainers);
        var containers = new List<GrfContainerSnapshot>();
        var limitReached = false;

        foreach (var file in SafeFileSystem.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
        {
            if (!ContainerExtensions.Contains(file.Extension))
            {
                continue;
            }

            if (containers.Count >= limit)
            {
                limitReached = true;
                break;
            }

            containers.Add(new GrfContainerSnapshot(
                file.FullName,
                file.Name,
                file.Extension,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)));
        }

        if (limitReached)
        {
            warnings.Add($"Container scan stopped at the configured limit of {limit} entries.");
        }

        var topLevelDirectoryCount = Directory.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly).Count();

        return new GrfRepositoryDiscoveryResult(
            rootPath,
            true,
            topLevelDirectoryCount,
            containers.Count,
            limitReached,
            containers.OrderByDescending(container => container.Length).ToArray(),
            warnings);
    }
}

