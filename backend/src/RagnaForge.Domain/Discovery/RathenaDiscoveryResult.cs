using RagnaForge.Domain.Configuration;

namespace RagnaForge.Domain.Discovery;

public sealed record ImportDatabaseStats(
    string RelativePath,
    bool Exists,
    int ActiveEntryCount,
    long Length);

public sealed record MapServerConfigSnapshot(
    bool Exists,
    string? DbPath,
    bool? UseGrf,
    IReadOnlyList<string> Imports);

public sealed record NpcScriptConfigSnapshot(
    bool Exists,
    IReadOnlyList<string> ActiveScripts,
    IReadOnlyList<string> CommentedScripts);

public sealed record RathenaDiscoveryResult(
    string RootPath,
    bool Exists,
    EpisodeMode DetectedMode,
    string ModeReason,
    bool HasDbImport,
    bool HasNpcCustom,
    IReadOnlyList<ImportDatabaseStats> ImportDatabases,
    MapServerConfigSnapshot MapServerConfig,
    int ActiveMapCount,
    int CommentedMapCount,
    int MapIndexLineCount,
    NpcScriptConfigSnapshot CustomNpcScripts,
    IReadOnlyList<string> Warnings);

