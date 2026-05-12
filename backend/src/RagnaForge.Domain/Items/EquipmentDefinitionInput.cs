using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Visuals;

namespace RagnaForge.Domain.Items;

public sealed record EquipmentDefinitionInput(
    ItemDefinitionInput Item,
    IReadOnlyList<string> EquipLocations,
    string? VisualCategory,
    int? ViewId,
    string? ClientSymbolName,
    string? ClientSpriteName,
    IReadOnlyList<string> AllowedJobs,
    string? Gender,
    int? EquipLevelMin,
    int? EquipLevelMax,
    int? ArmorLevel,
    int? WeaponLevel,
    int? Defense,
    bool? Refineable,
    string? EquipScript,
    string? UnEquipScript,
    string? WeaponBaseType,
    string? WeaponHitSound);

public sealed record EquipmentDryRunReport(
    DateTimeOffset GeneratedAtUtc,
    EpisodeProfile EpisodeProfile,
    string? ClientDateUsed,
    string ClientDateSource,
    string ClientItemMode,
    EquipmentDefinitionInput Input,
    int ResolvedId,
    bool CanApply,
    IReadOnlyList<ItemDependency> Dependencies,
    IReadOnlyList<ProposedFileChange> ProposedChanges,
    ItemDiffPreview DiffPreview,
    IReadOnlyList<string> Warnings,
    VisualThemeEvaluation? VisualTheme,
    GrfAssetLookupResult? ItemAssetLookup,
    GrfAssetLookupResult? VisualAssetLookup,
    ClientSidePlan? ClientSidePlan = null,
    ClientSidePlan? VisualClientSidePlan = null)
{
    public ClientSideMode ClientSideMode => ClientSidePlan?.ClientSideMode ?? ClientSideMode.Unknown;

    public IReadOnlyList<string> BytecodeBlocks =>
        (ClientSidePlan?.BytecodeBlockedFiles ?? [])
        .Concat(VisualClientSidePlan?.BytecodeBlockedFiles ?? [])
        .ToArray();

    public ClientApplyReadiness ApplyReadiness =>
        ClientSidePlan is { ApplyReadiness: ClientApplyReadiness.Blocked }
        || VisualClientSidePlan is { ApplyReadiness: ClientApplyReadiness.Blocked }
            ? ClientApplyReadiness.Blocked
            : ClientApplyReadiness.Ready;
}
