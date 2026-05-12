using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;

namespace RagnaForge.Domain.Monsters;

public sealed record MonsterApplyLog(
    string SchemaVersion,
    string OperationId,
    DateTimeOffset CreatedAtUtc,
    string WorkspaceRoot,
    string ApplyLogPath,
    string BackupRoot,
    RepositoryPaths RepositoryPaths,
    MonsterDefinitionInput Input,
    int ResolvedId,
    string Status,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record MonsterApplyResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool Applied,
    string ApplyLogPath,
    string BackupRoot,
    MonsterDryRunReport DryRunReport,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record MonsterRollbackResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool RolledBack,
    string RollbackLogPath,
    string ApplyLogPath,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages);
