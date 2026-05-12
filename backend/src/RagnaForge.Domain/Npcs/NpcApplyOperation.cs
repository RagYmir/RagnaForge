using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;

namespace RagnaForge.Domain.Npcs;

public sealed record NpcApplyLog(
    string SchemaVersion,
    string OperationId,
    DateTimeOffset CreatedAtUtc,
    string WorkspaceRoot,
    string ApplyLogPath,
    string BackupRoot,
    RepositoryPaths RepositoryPaths,
    NpcDefinitionInput Input,
    string Status,
    bool ServerOnlyApplied,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record NpcApplyResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool Applied,
    string ApplyLogPath,
    string BackupRoot,
    NpcDryRunReport DryRunReport,
    bool ServerOnlyApplied,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyConflict> Conflicts,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages,
    PostWriteValidationSummary? PostWriteValidation = null);

public sealed record NpcRollbackResult(
    DateTimeOffset GeneratedAtUtc,
    string OperationId,
    bool RolledBack,
    string RollbackLogPath,
    string ApplyLogPath,
    IReadOnlyList<ItemAppliedFileSnapshot> Files,
    IReadOnlyList<ItemApplyAuditEntry> AuditTrail,
    IReadOnlyList<string> Messages);
