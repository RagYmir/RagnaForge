namespace RagnaForge.Domain.Discovery;

public sealed record DataIniEntry(
    int Priority,
    string Value,
    bool ExistsOnDisk);

public sealed record AssetExtensionCount(
    string Extension,
    int Count);

public sealed record ClientDateDetection(
    string? Value,
    string Source,
    bool IsConfirmed);

public sealed record ClientItemDataModeSnapshot(
    bool UsesLegacyTables,
    bool UsesModernItemInfo,
    IReadOnlyList<FileSnapshot> ItemInfoFiles);

public sealed record PatchDiscoveryResult(
    string RootPath,
    bool Exists,
    IReadOnlyList<DataIniEntry> DataIniEntries,
    IReadOnlyList<FileSnapshot> LegacyItemTables,
    ClientItemDataModeSnapshot ItemDataMode,
    IReadOnlyList<FileSnapshot> DatainfoFiles,
    IReadOnlyList<FileSnapshot> TopLevelContainers,
    IReadOnlyList<FileSnapshot> ClientExecutables,
    ClientDateDetection ClientDate,
    IReadOnlyList<AssetExtensionCount> AssetCounts,
    IReadOnlyList<string> Warnings);
