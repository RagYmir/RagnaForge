using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;

namespace RagnaForge.Domain.Monsters;

public enum MonsterApplyReadiness
{
    Ready = 0,
    ReadyWithWarnings = 1,
    Blocked = 2
}

public sealed record MonsterSpawnDefinition(
    int X,
    int Y,
    int AreaX,
    int AreaY,
    string? Label,
    string? MapName = null,
    int? Amount = null,
    int? RespawnMilliseconds = null,
    string? EventLabel = null,
    bool Randomize = false);

public sealed record MonsterSkillDefinition(
    int SkillId,
    int SkillLevel,
    string State,
    int Rate,
    int CastTimeMilliseconds,
    int DelayMilliseconds,
    bool Cancelable,
    string Target,
    string ConditionType,
    int ConditionValue,
    int? Value1 = null,
    int? Value2 = null,
    int? Value3 = null,
    int? Value4 = null,
    int? Value5 = null,
    int? Emotion = null,
    string? Chat = null,
    string? Anchor = null,
    IReadOnlyDictionary<string, string>? UnsupportedFields = null);

public sealed record MonsterDropDefinition(
    int? ItemId,
    string? ItemAegisName,
    int Chance,
    int? Quantity = null,
    bool IsMvp = false,
    string? Kind = null);

public sealed record MonsterDefinitionInput(
    int? Id,
    string AegisName,
    string DisplayName,
    string MapName,
    int Level,
    int Hp,
    int Amount,
    int RespawnMilliseconds,
    string? SpriteOverride,
    string? FileSlug,
    MonsterSpawnDefinition Spawn,
    MonsterSkillDefinition? Skill,
    IReadOnlyList<MonsterDropDefinition>? Drops = null,
    IReadOnlyList<MonsterSkillDefinition>? Skills = null,
    IReadOnlyList<MonsterSpawnDefinition>? Spawns = null,
    bool AllowFutureDropReferences = false);

public sealed record MonsterDropPlan(
    string ItemReference,
    int? ResolvedItemId,
    string? ResolvedAegisName,
    int Chance,
    int Quantity,
    bool IsMvp,
    string? Kind,
    bool Exists,
    string Source);

public sealed record MonsterSkillPlan(
    int SkillId,
    int SkillLevel,
    string State,
    string Target,
    string ConditionType,
    int ConditionValue,
    string Anchor,
    bool Supported,
    IReadOnlyList<string> Notes);

public sealed record MonsterSpawnPlan(
    string MapName,
    int X,
    int Y,
    int AreaX,
    int AreaY,
    int Amount,
    int RespawnMilliseconds,
    string? Label,
    string? EventLabel,
    bool Randomize,
    IReadOnlyList<string> Notes);

public sealed record MonsterDryRunReport(
    DateTimeOffset GeneratedAtUtc,
    EpisodeProfile EpisodeProfile,
    MonsterDefinitionInput Input,
    int ResolvedId,
    bool CanApply,
    IReadOnlyList<ItemDependency> Dependencies,
    IReadOnlyList<ProposedFileChange> ProposedChanges,
    ItemDiffPreview DiffPreview,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MonsterDropPlan> Drops,
    IReadOnlyList<MonsterSkillPlan> Skills,
    IReadOnlyList<MonsterSpawnPlan> Spawns,
    IReadOnlyList<string> UnsupportedFields,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors,
    MonsterApplyReadiness ApplyReadiness,
    IReadOnlyList<PostWriteValidationPlanEntry> PostWriteValidationPlan);
