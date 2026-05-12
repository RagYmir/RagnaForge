using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;

namespace RagnaForge.Domain.Npcs;

public enum NpcClientFileFormat
{
    Missing = 0,
    TextLua = 1,
    TextLub = 2,
    BinaryLub = 3,
    LegacyTxt = 4,
    Unknown = 5
}

public enum NpcApplyReadiness
{
    Ready = 0,
    ReadyServerOnly = 1,
    Blocked = 2
}

public sealed record NpcDefinitionInput(
    string Name,
    string MapName,
    int X,
    int Y,
    int Direction,
    string Sprite,
    string? ScriptBody,
    string? FileSlug,
    string? ClientSymbolName = null,
    int? ClientIdentityId = null);

public sealed record NpcSpriteValidation(
    string Sprite,
    bool IsStandardClientSprite,
    bool RequiresAdditionalClientValidation,
    string DetectionSource,
    IReadOnlyList<string> Evidence,
    GrfAssetLookupResult? AssetLookup);

public sealed record NpcSpriteResolution(
    string SpriteName,
    bool Resolved,
    bool Ambiguous,
    string Source,
    string? Path,
    IReadOnlyList<string> Candidates,
    bool NeedsAssetCopyPlan);

public sealed record NpcClientIdentityFileDetection(
    string LogicalName,
    string? Path,
    NpcClientFileFormat Format,
    bool Selected,
    bool SupportedForApply);

public sealed record NpcClientIdentityPlan(
    bool Required,
    bool CanApply,
    IReadOnlyList<string> BlockReasons,
    IReadOnlyList<NpcClientIdentityFileDetection> FilesDetected,
    IReadOnlyList<string> FileFormats,
    string SpriteName,
    bool SpriteResolved,
    string SpriteSource,
    string? SpritePath,
    bool ExistingRegistration,
    IReadOnlyList<string> ExistingClientRegistration,
    IReadOnlyList<string> ProposedRegistrations,
    IReadOnlyList<string> UnsupportedFiles,
    IReadOnlyList<string> BytecodeBlockedFiles,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<ItemDiffPreviewEntry> DiffHunks,
    IReadOnlyList<string> ApplyTargets,
    IReadOnlyList<string> RollbackTargets,
    IReadOnlyList<PostWriteValidationPlanEntry> PostWriteValidationPlan)
{
    public bool CanApplyClientIdentity => CanApply;

    public IReadOnlyList<string> RequiredClientFiles =>
        FilesDetected
            .Where(file => file.Selected)
            .Select(file => file.Path ?? file.LogicalName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record NpcDryRunReport(
    DateTimeOffset GeneratedAtUtc,
    EpisodeProfile EpisodeProfile,
    NpcDefinitionInput Input,
    bool CanApply,
    IReadOnlyList<ItemDependency> Dependencies,
    IReadOnlyList<ProposedFileChange> ProposedChanges,
    ItemDiffPreview DiffPreview,
    IReadOnlyList<string> Warnings,
    NpcSpriteValidation SpriteValidation,
    NpcSpriteResolution SpriteResolution,
    bool ServerCanApply,
    NpcApplyReadiness ApplyReadiness,
    NpcClientIdentityPlan ClientIdentityPlan)
{
    public bool ClientIdentityRequired => ClientIdentityPlan.Required;
    public IReadOnlyList<string> RequiredClientFiles => ClientIdentityPlan.RequiredClientFiles;
    public bool ExistingClientRegistration => ClientIdentityPlan.ExistingRegistration;
    public IReadOnlyList<string> ExistingClientRegistrationDetails => ClientIdentityPlan.ExistingClientRegistration;
    public IReadOnlyList<string> ProposedClientRegistration => ClientIdentityPlan.ProposedRegistrations;
    public IReadOnlyList<string> BytecodeBlocks => ClientIdentityPlan.BytecodeBlockedFiles;
    public bool CanApplyClientIdentity => ClientIdentityPlan.CanApplyClientIdentity;
    public IReadOnlyList<PostWriteValidationPlanEntry> PostWriteValidationPlan => ClientIdentityPlan.PostWriteValidationPlan;
}
