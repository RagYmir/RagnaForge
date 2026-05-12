using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IGrfContainerIndexStore
{
    string BuildDefaultIndexPath(string containerPath);

    GrfContainerContentIndexDocument? TryLoad(string path);

    void Save(string path, GrfContainerContentIndexDocument document, bool overwrite);
}
