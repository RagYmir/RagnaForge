namespace RagnaForge.Domain.Discovery;

public sealed record GrfContainerSnapshot(
    string FullPath,
    string Name,
    string Extension,
    long Length,
    DateTimeOffset LastWriteTimeUtc);

public sealed record GrfRepositoryDiscoveryResult(
    string RootPath,
    bool Exists,
    int TopLevelDirectoryCount,
    int ContainerCount,
    bool ContainerLimitReached,
    IReadOnlyList<GrfContainerSnapshot> Containers,
    IReadOnlyList<string> Warnings);

