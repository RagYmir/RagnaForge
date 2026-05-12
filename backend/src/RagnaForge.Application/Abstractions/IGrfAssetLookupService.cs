using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Abstractions;

public interface IGrfAssetLookupService
{
    GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options);
}
