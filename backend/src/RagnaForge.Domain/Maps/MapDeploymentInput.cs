using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;

namespace RagnaForge.Domain.Maps;

public sealed record MapDeploymentInput(
    string MapName,
    string? RswResourceName,
    string? GndResourceName,
    string? GatResourceName,
    string? FileSlug);

public sealed record MapAssetPlan(
    string Category,
    string RelativePath,
    string TargetPath,
    bool ExistsInTarget,
    string SourceKind,
    string? SourcePath,
    GrfAssetLookupMatch? GrfMatch,
    bool Required,
    bool NeedsCopy);

public sealed record MapCachePlan(
    bool ToolDetected,
    string? ToolPath,
    string CachePath,
    bool ExistingCacheDetected,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> Notes);

public sealed record MapReferencedAsset(
    string Category,
    string ReferencePath,
    string SourceFileType,
    bool ExistsInLoosePatch,
    string? LoosePath,
    GrfAssetLookupResult? Lookup,
    bool Resolved);

public sealed record MapDependencyScanResult(
    bool DeepScanAvailable,
    string Source,
    IReadOnlyList<MapReferencedAsset> ReferencedAssets,
    IReadOnlyList<string> Warnings);

public sealed record MapDryRunReport(
    DateTimeOffset GeneratedAtUtc,
    EpisodeProfile EpisodeProfile,
    MapDeploymentInput Input,
    bool CanApply,
    IReadOnlyList<ItemDependency> Dependencies,
    IReadOnlyList<ProposedFileChange> ProposedChanges,
    ItemDiffPreview DiffPreview,
    IReadOnlyList<string> Warnings,
    GrfAssetLookupResult? RswLookup,
    GrfAssetLookupResult? GndLookup,
    GrfAssetLookupResult? GatLookup,
    MapDependencyScanResult? DependencyScan,
    IReadOnlyList<MapAssetPlan> AssetPlans,
    MapCachePlan? MapCachePlan);

public sealed record MapCacheBuildRequest(
    string MapName,
    string SeedCachePath,
    string OutputCachePath,
    string WorkingRoot);

public sealed record MapCacheBuildResult(
    DateTimeOffset GeneratedAtUtc,
    bool Succeeded,
    string ToolPath,
    string OutputCachePath,
    int ExitCode,
    bool MapPresentInCache,
    IReadOnlyList<string> Messages);

public sealed record MapAppliedFileSnapshot(
    string TargetPath,
    string BackupPath,
    bool ExistedBefore,
    string ChangeKind,
    bool IsBinary,
    long PreviousLength,
    long NewLength,
    string? PreviousSha256,
    string NewSha256,
    string? BackupSha256,
    string? SourceSummary);

public sealed record MapApplyLog(
    string SchemaVersion,
    string OperationId,
    DateTimeOffset CreatedAtUtc,
    string WorkspaceRoot,
    string ApplyLogPath,
    string BackupRoot,
    RepositoryPaths RepositoryPaths,
    MapDeploymentInput Input,
    string Status,
    IReadOnlyList<MapAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    IReadOnlyList<MapAssetPlan> AssetPlans,
    MapCachePlan? MapCachePlan);

public sealed record MapApplyResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool Applied,
    string ApplyLogPath,
    string BackupRoot,
    MapDryRunReport DryRunReport,
    IReadOnlyList<MapAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages);

public sealed record MapRollbackResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool RolledBack,
    string RollbackLogPath,
    string ApplyLogPath,
    IReadOnlyList<MapAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages);
