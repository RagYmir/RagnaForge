using RagnaForge.Domain.Configuration;

namespace RagnaForge.Domain.Items;

public sealed record EquipmentApplyLog(
    string SchemaVersion,
    string OperationId,
    DateTimeOffset CreatedAtUtc,
    string WorkspaceRoot,
    string ApplyLogPath,
    string BackupRoot,
    RepositoryPaths RepositoryPaths,
    EquipmentDefinitionInput Input,
    int ResolvedId,
    string Status,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record EquipmentApplyResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool Applied,
    string ApplyLogPath,
    string BackupRoot,
    EquipmentDryRunReport DryRunReport,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record EquipmentRollbackResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool RolledBack,
    string RollbackLogPath,
    string ApplyLogPath,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages);
