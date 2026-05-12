using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IGrfRepositoryIndexStore
{
    string DefaultIndexPath { get; }

    GrfRepositoryIndexDocument? TryLoad(string path);

    void Save(string path, GrfRepositoryIndexDocument document, bool overwrite);
}
