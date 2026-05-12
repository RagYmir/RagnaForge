namespace RagnaForge.Domain.Discovery;

public sealed record GrfContentEntrySnapshot(
    string RelativePath,
    string DirectoryPath,
    string FileName,
    string Extension,
    int SizeCompressed,
    int SizeDecompressed,
    bool Encrypted);

public sealed record GrfExtensionCount(
    string Extension,
    int Count);

public sealed record GrfTopLevelDirectoryCount(
    string DirectoryName,
    int Count);

public sealed record GrfContainerContentIndexDocument(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string ContainerPath,
    string ContainerName,
    string ContainerType,
    long ContainerLength,
    DateTimeOffset? ContainerLastWriteTimeUtc,
    int EntryCount,
    int DirectoryCount,
    int MaxEntriesCaptured,
    bool IsTruncated,
    IReadOnlyList<GrfExtensionCount> ExtensionCounts,
    IReadOnlyList<GrfTopLevelDirectoryCount> TopLevelDirectories,
    IReadOnlyList<GrfContentEntrySnapshot> Entries)
{
    public const string CurrentSchemaVersion = "1.0";

    public static GrfContainerContentIndexDocument Create(
        string containerPath,
        long containerLength,
        DateTimeOffset? containerLastWriteTimeUtc,
        int entryCount,
        int directoryCount,
        int maxEntriesCaptured,
        bool isTruncated,
        IReadOnlyList<GrfExtensionCount> extensionCounts,
        IReadOnlyList<GrfTopLevelDirectoryCount> topLevelDirectories,
        IReadOnlyList<GrfContentEntrySnapshot> entries,
        DateTimeOffset? generatedAtUtc = null) =>
        new(
            CurrentSchemaVersion,
            generatedAtUtc ?? DateTimeOffset.UtcNow,
            containerPath,
            Path.GetFileName(containerPath),
            Path.GetExtension(containerPath).TrimStart('.').ToLowerInvariant(),
            containerLength,
            containerLastWriteTimeUtc,
            entryCount,
            directoryCount,
            maxEntriesCaptured,
            isTruncated,
            extensionCounts,
            topLevelDirectories,
            entries);
}

public sealed record GrfContainerInspectionResult(
    GrfContainerContentIndexDocument Index,
    string Engine,
    string HostExecutablePath,
    IReadOnlyList<string> Warnings);
