using RagnaForge.Domain.Configuration;

namespace RagnaForge.Domain.Items;

public sealed record ItemAppliedFileSnapshot(
    string TargetPath,
    string BackupPath,
    bool ExistedBefore,
    string ChangeKind,
    int PreviousLength,
    int NewLength,
    int PreviousLineCount,
    int NewLineCount,
    string? PreviousSha256,
    string? NewSha256,
    string? BackupSha256,
    string PreviewSha256,
    string? FirstPreviewLine);

public sealed record ItemApplyConflict(
    string TargetPath,
    string ChangeKind,
    string Code,
    string Message,
    string? Evidence = null);

public sealed record ItemApplyAuditEntry(
    DateTimeOffset GeneratedAtUtc,
    string Stage,
    string Message,
    string? TargetPath = null);

public sealed record ItemApplyLog(
    string SchemaVersion,
    string OperationId,
    DateTimeOffset CreatedAtUtc,
    string WorkspaceRoot,
    string ApplyLogPath,
    string BackupRoot,
    RepositoryPaths RepositoryPaths,
    ItemDefinitionInput Input,
    int ResolvedId,
    string Status,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record ItemApplyResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool Applied,
    string ApplyLogPath,
    string BackupRoot,
    ItemDryRunReport DryRunReport,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record ItemRollbackResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool RolledBack,
    string RollbackLogPath,
    string ApplyLogPath,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages);
