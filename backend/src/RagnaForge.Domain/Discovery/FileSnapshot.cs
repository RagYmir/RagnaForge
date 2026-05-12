namespace RagnaForge.Domain.Discovery;

public sealed record FileSnapshot(
    string FullPath,
    string Name,
    bool Exists,
    long Length,
    DateTimeOffset? LastWriteTimeUtc);

