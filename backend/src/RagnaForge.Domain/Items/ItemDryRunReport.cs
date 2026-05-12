using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Domain.Items;

public enum ItemDependencyState
{
    Satisfied = 0,
    Warning = 1,
    Missing = 2,
    Proposed = 3
}

public sealed record ItemDependency(
    string Category,
    ItemDependencyState State,
    string Message,
    string? SourcePath = null);

public sealed record ProposedFileChange(
    string TargetPath,
    string ChangeKind,
    bool Exists,
    string Preview);

public sealed record ItemDiffPreviewEntry(
    string TargetPath,
    string ChangeKind,
    bool Exists,
    int ExistingLineCount,
    int AddedLineCount,
    string UnifiedDiff);

public sealed record ItemDiffPreview(
    int FileCount,
    int CreatedCount,
    int UpdatedCount,
    IReadOnlyList<ItemDiffPreviewEntry> Entries);

public sealed record ItemDryRunReport(
    DateTimeOffset GeneratedAtUtc,
    EpisodeProfile EpisodeProfile,
    string? ClientDateUsed,
    string ClientDateSource,
    string ClientItemMode,
    ItemDefinitionInput Input,
    int ResolvedId,
    bool CanApply,
    IReadOnlyList<ItemDependency> Dependencies,
    IReadOnlyList<ProposedFileChange> ProposedChanges,
    ItemDiffPreview DiffPreview,
    IReadOnlyList<string> Warnings,
    GrfAssetLookupResult? AssetLookup,
    ClientSidePlan? ClientSidePlan = null)
{
    public ClientSideMode ClientSideMode => ClientSidePlan?.ClientSideMode ?? ClientSideMode.Unknown;

    public IReadOnlyList<string> BytecodeBlocks => ClientSidePlan?.BytecodeBlockedFiles ?? [];

    public IReadOnlyList<string> ExistingClientRegistration => ClientSidePlan?.ExistingRegistrations ?? [];

    public IReadOnlyList<string> ProposedClientRegistration => ClientSidePlan?.ProposedRegistrations ?? [];

    public IReadOnlyList<string> PostWriteValidationPlan => ClientSidePlan?.PostWriteValidationPlan ?? [];
}
