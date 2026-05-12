namespace RagnaForge.Domain.Assets;

public enum GrfAssetLookupSource
{
    None = 0,
    LocalIndex = 1,
    LiveScan = 2,
    LiveScanFallback = 3
}

public sealed record GrfAssetLookupOptions(
    bool Enabled,
    IReadOnlyList<string> ContainerPaths,
    int MaxContainers,
    int MaxMatchesPerContainer,
    bool AllowContainsMatch = false,
    IReadOnlyList<string>? NameHints = null)
{
    public static GrfAssetLookupOptions Disabled { get; } = new(false, [], 0, 0);
}

public sealed record GrfAssetLookupMatch(
    string ContainerPath,
    string RelativePath,
    string Extension,
    int SizeCompressed,
    int SizeDecompressed,
    bool Encrypted);

public sealed record GrfAssetLookupResult(
    string ResourceName,
    bool Searched,
    int ContainersScanned,
    int ContainersRequested,
    IReadOnlyList<GrfAssetLookupMatch> Matches,
    IReadOnlyList<string> Warnings,
    GrfAssetLookupSource Source = GrfAssetLookupSource.None,
    int LocalIndexesLoaded = 0,
    int LiveContainersScanned = 0);
