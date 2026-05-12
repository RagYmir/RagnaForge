using RagnaForge.Domain.Configuration;

namespace RagnaForge.Api;

public sealed record ConfigRequest(string? ConfigPath = null);

public sealed record DiscoveryRequest(
    string? ConfigPath = null,
    RepositoryPaths? Paths = null,
    EpisodeProfile? EpisodeProfile = null,
    int MaxGrfContainers = 200);

public sealed record GrfIndexRequest(
    string? ConfigPath = null,
    string? GrfRepositoryPath = null,
    string? CachePath = null,
    int MaxContainers = 200,
    bool ForceRefresh = false,
    bool SaveCache = true);

public sealed record GrfInspectRequest(
    string Container,
    string? ConfigPath = null,
    string? GrfEditorPath = null,
    string? GrfRepositoryPath = null,
    string? CachePath = null,
    int Limit = 200,
    bool Force = false,
    bool SaveCache = true);

public abstract record PipelineRequest(
    string? ConfigPath = null,
    string? AssetGrfContainer = null,
    bool ScanGrfAssets = false,
    int MaxGrfAssetContainers = 5,
    int MaxGrfAssetMatches = 10,
    string? GrfCachePath = null);

public sealed record ItemDryRunRequest(
    string AegisName,
    string DisplayName,
    int? Id = null,
    string? ResourceName = null,
    string Type = "Etc",
    int Buy = 0,
    int Sell = 0,
    int Weight = 0,
    int Slots = 0,
    string? Script = null,
    IReadOnlyList<string>? IdentifiedDescriptionLines = null,
    string? UnidentifiedDisplayName = null,
    string? UnidentifiedResourceName = null,
    IReadOnlyList<string>? UnidentifiedDescriptionLines = null,
    string? ConfigPath = null,
    string? AssetGrfContainer = null,
    bool ScanGrfAssets = false,
    int MaxGrfAssetContainers = 5,
    int MaxGrfAssetMatches = 10,
    string? GrfCachePath = null) : PipelineRequest(ConfigPath, AssetGrfContainer, ScanGrfAssets, MaxGrfAssetContainers, MaxGrfAssetMatches, GrfCachePath);

public sealed record EquipmentDryRunRequest(
    string AegisName,
    string DisplayName,
    IReadOnlyList<string> EquipLocations,
    int? Id = null,
    string? ResourceName = null,
    string Type = "Armor",
    int Buy = 0,
    int Sell = 0,
    int Weight = 0,
    int Slots = 0,
    string? Script = null,
    IReadOnlyList<string>? IdentifiedDescriptionLines = null,
    string? UnidentifiedDisplayName = null,
    string? UnidentifiedResourceName = null,
    IReadOnlyList<string>? UnidentifiedDescriptionLines = null,
    string? VisualCategory = null,
    int? ViewId = null,
    string? ClientSymbolName = null,
    string? ClientSpriteName = null,
    IReadOnlyList<string>? AllowedJobs = null,
    string? Gender = null,
    int? EquipLevelMin = null,
    int? EquipLevelMax = null,
    int? ArmorLevel = null,
    int? WeaponLevel = null,
    int? Defense = null,
    bool? Refineable = null,
    string? EquipScript = null,
    string? UnEquipScript = null,
    string? WeaponBaseType = null,
    string? WeaponHitSound = null,
    string? ConfigPath = null,
    string? AssetGrfContainer = null,
    bool ScanGrfAssets = false,
    int MaxGrfAssetContainers = 5,
    int MaxGrfAssetMatches = 10,
    string? GrfCachePath = null) : PipelineRequest(ConfigPath, AssetGrfContainer, ScanGrfAssets, MaxGrfAssetContainers, MaxGrfAssetMatches, GrfCachePath);

public sealed record NpcDryRunRequest(
    string Name,
    string MapName,
    int X,
    int Y,
    int Direction = 2,
    string Sprite = "4_M_JOB_BLACKSMITH",
    string? ScriptBody = null,
    string? FileSlug = null,
    string? ClientSymbolName = null,
    int? ClientIdentityId = null,
    string? ConfigPath = null,
    string? AssetGrfContainer = null,
    bool ScanGrfAssets = false,
    int MaxGrfAssetContainers = 5,
    int MaxGrfAssetMatches = 10,
    string? GrfCachePath = null) : PipelineRequest(ConfigPath, AssetGrfContainer, ScanGrfAssets, MaxGrfAssetContainers, MaxGrfAssetMatches, GrfCachePath);

public sealed record MonsterDropRequest(
    int? ItemId = null,
    string? ItemAegisName = null,
    int Chance = 100,
    int? Quantity = null,
    bool IsMvp = false,
    string? Kind = null);

public sealed record MonsterSkillRequest(
    int SkillId,
    int SkillLevel = 1,
    string State = "any",
    int Rate = 10000,
    int CastTimeMilliseconds = 0,
    int DelayMilliseconds = 5000,
    bool Cancelable = false,
    string Target = "target",
    string ConditionType = "always",
    int ConditionValue = 0,
    int? Value1 = null,
    int? Value2 = null,
    int? Value3 = null,
    int? Value4 = null,
    int? Value5 = null,
    int? Emotion = null,
    string? Chat = null,
    string? Anchor = null,
    IReadOnlyDictionary<string, string>? UnsupportedFields = null);

public sealed record MonsterSpawnRequest(
    int X = 0,
    int Y = 0,
    int AreaX = 0,
    int AreaY = 0,
    string? Label = null,
    string? MapName = null,
    int? Amount = null,
    int? RespawnMilliseconds = null,
    string? EventLabel = null,
    bool Randomize = false);

public sealed record MonsterDryRunRequest(
    string AegisName,
    string DisplayName,
    string MapName,
    int? Id = null,
    int Level = 1,
    int Hp = 10,
    int Amount = 1,
    int RespawnMilliseconds = 60000,
    string? SpriteOverride = null,
    string? FileSlug = null,
    MonsterSpawnRequest? Spawn = null,
    MonsterSkillRequest? Skill = null,
    IReadOnlyList<MonsterDropRequest>? Drops = null,
    IReadOnlyList<MonsterSkillRequest>? Skills = null,
    IReadOnlyList<MonsterSpawnRequest>? Spawns = null,
    bool AllowFutureDropReferences = false,
    string? ConfigPath = null) : PipelineRequest(ConfigPath);

public sealed record MapDryRunRequest(
    string MapName,
    string? RswResourceName = null,
    string? GndResourceName = null,
    string? GatResourceName = null,
    string? FileSlug = null,
    string? ConfigPath = null,
    string? AssetGrfContainer = null,
    bool ScanGrfAssets = false,
    int MaxGrfAssetContainers = 5,
    int MaxGrfAssetMatches = 10,
    string? GrfCachePath = null) : PipelineRequest(ConfigPath, AssetGrfContainer, ScanGrfAssets, MaxGrfAssetContainers, MaxGrfAssetMatches, GrfCachePath);
