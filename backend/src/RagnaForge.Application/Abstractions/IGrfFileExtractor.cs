using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Abstractions;

public interface IGrfFileExtractor
{
    GrfFileExtractionResult ExtractFiles(
        RepositoryPaths paths,
        IReadOnlyList<GrfAssetLookupMatch> matches,
        string extractionRoot,
        long maxFileBytes);
}
