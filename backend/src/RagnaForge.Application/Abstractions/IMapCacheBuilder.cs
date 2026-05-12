using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Maps;

namespace RagnaForge.Application.Abstractions;

public interface IMapCacheBuilder
{
    MapCacheBuildResult Build(
        RepositoryPaths paths,
        MapCacheBuildRequest request);
}
