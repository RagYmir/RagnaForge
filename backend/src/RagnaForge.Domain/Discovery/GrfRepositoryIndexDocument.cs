namespace RagnaForge.Domain.Discovery;

public sealed record GrfRepositoryIndexDocument(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string RootPath,
    int TopLevelDirectoryCount,
    int TotalContainerCount,
    IReadOnlyList<GrfContainerSnapshot> Containers)
{
    public const string CurrentSchemaVersion = "1.0";

    public static GrfRepositoryIndexDocument Create(
        string rootPath,
        int topLevelDirectoryCount,
        IReadOnlyList<GrfContainerSnapshot> containers,
        DateTimeOffset? generatedAtUtc = null) =>
        new(
            CurrentSchemaVersion,
            generatedAtUtc ?? DateTimeOffset.UtcNow,
            rootPath,
            topLevelDirectoryCount,
            containers.Count,
            containers);
}

public sealed record GrfRepositoryIndexSummary(
    string CachePath,
    bool CacheLoaded,
    bool CacheSaved,
    bool LimitReached,
    int PreviousContainerCount,
    int TotalContainerCount,
    int AddedCount,
    int ChangedCount,
    int UnchangedCount,
    int RemovedCount,
    int ReturnedContainerCount,
    IReadOnlyList<string> Warnings);

public sealed record GrfRepositoryIndexResult(
    GrfRepositoryIndexDocument Index,
    GrfRepositoryIndexSummary Summary);
