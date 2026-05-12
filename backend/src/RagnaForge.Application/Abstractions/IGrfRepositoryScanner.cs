using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IGrfRepositoryScanner
{
    GrfRepositoryDiscoveryResult Scan(string rootPath, int maxContainers);
}

