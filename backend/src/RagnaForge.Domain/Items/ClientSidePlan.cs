namespace RagnaForge.Domain.Items;

public enum ClientSideFileFormat
{
    Missing = 0,
    TextLua = 1,
    TextLub = 2,
    BinaryLub = 3,
    LegacyTxt = 4,
    Unknown = 5
}

public enum ClientSideMode
{
    Unknown = 0,
    ItemInfo = 1,
    LegacyTxt = 2,
    Hybrid = 3
}

public enum ClientApplyReadiness
{
    Ready = 0,
    Blocked = 1
}

public sealed record ClientSideFileDetection(
    string LogicalName,
    string Path,
    ClientSideFileFormat Format,
    bool Exists,
    bool Selected,
    bool SupportedForApply);

public sealed record ClientSidePlan(
    bool Required,
    bool CanApply,
    IReadOnlyList<string> BlockReasons,
    string ClientProfile,
    ClientSideMode ClientSideMode,
    IReadOnlyList<ClientSideFileDetection> DetectedFiles,
    IReadOnlyList<string> FileFormats,
    bool ItemInfoDetected,
    bool LegacyTablesDetected,
    bool HybridClientDetected,
    IReadOnlyList<string> SupportedTargets,
    IReadOnlyList<string> UnsupportedTargets,
    IReadOnlyList<string> BytecodeBlockedFiles,
    IReadOnlyList<string> ProposedRegistrations,
    IReadOnlyList<string> ExistingRegistrations,
    IReadOnlyList<ProposedFileChange> ProposedChanges,
    IReadOnlyList<string> DiffHunks,
    IReadOnlyList<string> ApplyTargets,
    IReadOnlyList<string> RollbackTargets,
    IReadOnlyList<string> PostWriteValidationPlan,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors,
    ClientApplyReadiness ApplyReadiness);
