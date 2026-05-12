using RagnaForge.Application.Abstractions;
using RagnaForge.Application.Configuration;
using RagnaForge.Application.Discovery;
using RagnaForge.Application.Grf;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Discovery;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Maps;
using RagnaForge.Domain.Monsters;
using RagnaForge.Domain.Npcs;
using RagnaForge.Infrastructure.Configuration;
using RagnaForge.Infrastructure.Grf;
using RagnaForge.Infrastructure.GrfEditorIntegration;
using RagnaForge.Infrastructure.Items;
using RagnaForge.Infrastructure.Maps;
using RagnaForge.Infrastructure.Monsters;
using RagnaForge.Infrastructure.Npcs;
using RagnaForge.Infrastructure.Patch;
using RagnaForge.Infrastructure.Rathena;

namespace RagnaForge.Api;

public sealed class RagnaForgeApiService
{
    private readonly string _workspaceRoot;
    private readonly RagnaForgeApiOptions _apiOptions;
    private readonly JsonConfigurationManifestStore _manifestStore;
    private readonly ConfigurationManifestValidator _manifestValidator = new();

    public RagnaForgeApiService(string workspaceRoot, RagnaForgeApiOptions? apiOptions = null)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _apiOptions = apiOptions ?? new RagnaForgeApiOptions();
        _manifestStore = new JsonConfigurationManifestStore(_workspaceRoot);
    }

    public object GetStatus() => new
    {
        Service = "RagnaForge API",
        Mode = "read-only-dry-run-diff-preview",
        WorkspaceRoot = _workspaceRoot,
        _apiOptions.ReadOnlyMode,
        ApplyEndpointsEnabled = _apiOptions.EnableApplyEndpoints && !_apiOptions.ReadOnlyMode,
        RollbackEndpointsEnabled = _apiOptions.EnableRollbackEndpoints && !_apiOptions.ReadOnlyMode,
        _apiOptions.RequireApiKey,
        _apiOptions.ApiKeyHeaderName,
        _apiOptions.MaxRequestBodyBytes,
        _apiOptions.MaxGrfContainersPerRequest,
        _apiOptions.MaxDiffHunksPerResponse,
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        ApiSafetyPolicy.DisabledWriteOperations,
        ApiSafetyPolicy.Capabilities
    };

    public object ValidateConfig(ConfigRequest request)
    {
        var manifestPath = ResolveManifestPath(request.ConfigPath);
        var manifest = _manifestStore.Load(manifestPath);
        var validation = _manifestValidator.Validate(manifest);

        return new
        {
            ManifestPath = Path.GetFullPath(manifestPath),
            Validation = validation,
            Manifest = manifest
        };
    }

    public RepositoryDiscoveryReport Discover(DiscoveryRequest request)
    {
        var options = CreateDiscoveryOptions(request);
        return CreateDiscoveryService().Run(options);
    }

    public GrfRepositoryIndexResult IndexGrfs(GrfIndexRequest request)
    {
        EnsureMax("MaxContainers", request.MaxContainers, _apiOptions.MaxGrfContainersPerRequest);
        var rootPath = request.GrfRepositoryPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = LoadValidatedManifest(request.ConfigPath).Paths.GrfRepositoryPath;
        }

        var store = new JsonGrfRepositoryIndexStore(_workspaceRoot);
        return new CachedGrfRepositoryIndexer(store).Build(
            new GrfRepositoryIndexOptions(
                rootPath,
                ResolveWorkspaceCachePath(request.CachePath ?? store.DefaultIndexPath),
                Math.Max(1, request.MaxContainers),
                request.ForceRefresh,
                request.SaveCache));
    }

    public object InspectGrf(GrfInspectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Container))
        {
            throw new InvalidOperationException("Container is required.");
        }

        var manifest = string.IsNullOrWhiteSpace(request.ConfigPath)
            ? null
            : LoadValidatedManifest(request.ConfigPath);

        var grfEditorPath = request.GrfEditorPath ?? manifest?.Paths.GrfEditorPath;
        if (string.IsNullOrWhiteSpace(grfEditorPath))
        {
            throw new InvalidOperationException("GRF Editor path is required when config is not provided.");
        }

        var repositoryPath = request.GrfRepositoryPath ?? manifest?.Paths.GrfRepositoryPath;
        var containerPath = ResolveContainerPath(repositoryPath, request.Container);
        var result = new GrfAssemblyContainerInspector().Inspect(
            grfEditorPath,
            containerPath,
            Math.Max(1, request.Limit));

        var store = new JsonGrfContainerIndexStore(_workspaceRoot);
        var indexPath = ResolveWorkspaceIndexPath(request.CachePath ?? store.BuildDefaultIndexPath(containerPath));
        if (request.SaveCache)
        {
            store.Save(indexPath, result.Index, request.Force);
        }

        return new
        {
            IndexPath = request.SaveCache ? Path.GetFullPath(indexPath) : null,
            Saved = request.SaveCache,
            Result = result
        };
    }

    public ItemDryRunReport CreateItemDryRun(ItemDryRunRequest request)
    {
        var manifest = LoadValidatedManifest(request.ConfigPath);
        var service = CreateItemDryRunService(request, manifest.Paths.GrfRepositoryPath);
        return service.Create(manifest.Paths, manifest.EpisodeProfile, ToItemInput(request));
    }

    public ItemDiffPreview CreateItemDiffPreview(ItemDryRunRequest request) =>
        CreateItemDryRun(request).DiffPreview;

    public EquipmentDryRunReport CreateEquipmentDryRun(EquipmentDryRunRequest request)
    {
        var manifest = LoadValidatedManifest(request.ConfigPath);
        var service = CreateEquipmentDryRunService(request, manifest.Paths.GrfRepositoryPath);
        return service.Create(manifest.Paths, manifest.EpisodeProfile, ToEquipmentInput(request));
    }

    public ItemDiffPreview CreateEquipmentDiffPreview(EquipmentDryRunRequest request) =>
        CreateEquipmentDryRun(request).DiffPreview;

    public NpcDryRunReport CreateNpcDryRun(NpcDryRunRequest request)
    {
        var manifest = LoadValidatedManifest(request.ConfigPath);
        var service = CreateNpcDryRunService(request, manifest.Paths.GrfRepositoryPath);
        return service.Create(manifest.Paths, manifest.EpisodeProfile, ToNpcInput(request));
    }

    public ItemDiffPreview CreateNpcDiffPreview(NpcDryRunRequest request) =>
        CreateNpcDryRun(request).DiffPreview;

    public MonsterDryRunReport CreateMonsterDryRun(MonsterDryRunRequest request)
    {
        var manifest = LoadValidatedManifest(request.ConfigPath);
        return new MonsterDryRunService().Create(manifest.Paths, manifest.EpisodeProfile, ToMonsterInput(request));
    }

    public ItemDiffPreview CreateMonsterDiffPreview(MonsterDryRunRequest request) =>
        CreateMonsterDryRun(request).DiffPreview;

    public MapDryRunReport CreateMapDryRun(MapDryRunRequest request)
    {
        var manifest = LoadValidatedManifest(request.ConfigPath);
        var grfLookupOptions = BuildGrfLookupOptions(request, manifest.Paths.GrfRepositoryPath);
        var service = grfLookupOptions.Enabled
            ? new MapDryRunService(
                CreateGrfAssetLookupService(),
                grfLookupOptions,
                new GrfAssemblyFileExtractor(),
                Path.Combine(_workspaceRoot, "tmp", "map-dependency-scan-api"))
            : new MapDryRunService();

        return service.Create(manifest.Paths, manifest.EpisodeProfile, ToMapInput(request));
    }

    public ItemDiffPreview CreateMapDiffPreview(MapDryRunRequest request) =>
        CreateMapDryRun(request).DiffPreview;

    private DiscoveryOptions CreateDiscoveryOptions(DiscoveryRequest request)
    {
        if (request.Paths is not null)
        {
            return new DiscoveryOptions(
                request.Paths,
                request.EpisodeProfile ?? new EpisodeProfile("api-request", EpisodeMode.Unknown, null, "Explicit API discovery request."),
                Math.Max(1, request.MaxGrfContainers));
        }

        var manifest = LoadValidatedManifest(request.ConfigPath);
        return new DiscoveryOptions(
            manifest.Paths,
            manifest.EpisodeProfile,
            Math.Max(1, request.MaxGrfContainers));
    }

    private ConfigurationManifest LoadValidatedManifest(string? configPath)
    {
        var manifestPath = ResolveManifestPath(configPath);
        var manifest = _manifestStore.Load(manifestPath);
        var validation = _manifestValidator.Validate(manifest);

        if (!validation.IsValid)
        {
            throw new InvalidOperationException("Manifest validation failed: " + string.Join("; ", validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }

        return manifest;
    }

    private LegacyItemDryRunService CreateItemDryRunService(PipelineRequest request, string grfRepositoryPath)
    {
        var grfLookupOptions = BuildGrfLookupOptions(request, grfRepositoryPath);
        return grfLookupOptions.Enabled
            ? new LegacyItemDryRunService(CreateGrfAssetLookupService(), grfLookupOptions)
            : new LegacyItemDryRunService();
    }

    private LegacyEquipmentDryRunService CreateEquipmentDryRunService(PipelineRequest request, string grfRepositoryPath)
    {
        var grfLookupOptions = BuildGrfLookupOptions(request, grfRepositoryPath);
        return grfLookupOptions.Enabled
            ? new LegacyEquipmentDryRunService(CreateGrfAssetLookupService(), grfLookupOptions)
            : new LegacyEquipmentDryRunService();
    }

    private NpcDryRunService CreateNpcDryRunService(PipelineRequest request, string grfRepositoryPath)
    {
        var grfLookupOptions = BuildGrfLookupOptions(request, grfRepositoryPath);
        return grfLookupOptions.Enabled
            ? new NpcDryRunService(CreateGrfAssetLookupService(), grfLookupOptions)
            : new NpcDryRunService();
    }

    private GrfAssetLookupOptions BuildGrfLookupOptions(PipelineRequest request, string grfRepositoryPath)
    {
        EnsureMax("MaxGrfAssetContainers", request.MaxGrfAssetContainers, _apiOptions.MaxGrfContainersPerRequest);
        var enabled = request.ScanGrfAssets || !string.IsNullOrWhiteSpace(request.AssetGrfContainer);
        if (!enabled)
        {
            return GrfAssetLookupOptions.Disabled;
        }

        var containers = !string.IsNullOrWhiteSpace(request.AssetGrfContainer)
            ? request.AssetGrfContainer
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(container => ResolveContainerPath(grfRepositoryPath, container))
                .ToArray()
            : LoadGrfAssetContainersFromCache(request, Math.Max(1, request.MaxGrfAssetContainers));

        return new GrfAssetLookupOptions(
            true,
            containers,
            Math.Max(1, request.MaxGrfAssetContainers),
            Math.Max(1, request.MaxGrfAssetMatches));
    }

    private IReadOnlyList<string> LoadGrfAssetContainersFromCache(PipelineRequest request, int maxContainers)
    {
        var store = new JsonGrfRepositoryIndexStore(_workspaceRoot);
        var cachePath = ResolveWorkspaceCachePath(request.GrfCachePath ?? store.DefaultIndexPath);
        var document = store.TryLoad(cachePath);
        if (document is null)
        {
            return [];
        }

        return document.Containers
            .Where(container => container.Extension.Equals(".grf", StringComparison.OrdinalIgnoreCase)
                                || container.Extension.Equals(".gpf", StringComparison.OrdinalIgnoreCase))
            .Take(maxContainers)
            .Select(container => container.FullPath)
            .ToArray();
    }

    private string ResolveManifestPath(string? configPath) =>
        ResolveWorkspacePath(configPath ?? _manifestStore.DefaultManifestPath, Path.Combine(_workspaceRoot, "data", "manifests"));

    private string ResolveWorkspaceCachePath(string path) =>
        ResolveWorkspacePath(path, Path.Combine(_workspaceRoot, "data", "cache"));

    private string ResolveWorkspaceIndexPath(string path) =>
        ResolveWorkspacePath(path, Path.Combine(_workspaceRoot, "data", "indexes"));

    private string ResolveWorkspacePath(string path, params string[] allowedRoots)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(_workspaceRoot, path));
        if (!IsInside(fullPath, _workspaceRoot))
        {
            throw ApiException.BadRequest("path.outside_workspace", "Workspace-relative API paths must stay inside the RagnaForge workspace.");
        }

        if (allowedRoots.Length > 0 && !allowedRoots.Any(root => IsInside(fullPath, root)))
        {
            throw ApiException.BadRequest("path.disallowed_root", "Requested workspace path is outside the allowed API directory for this operation.");
        }

        return fullPath;
    }

    private static bool IsInside(string candidate, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCandidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureMax(string name, int value, int max)
    {
        if (value > max)
        {
            throw ApiException.Unprocessable(
                $"limit.{name.ToLowerInvariant()}",
                $"{name} '{value}' exceeds configured maximum '{max}'.");
        }
    }

    private static string ResolveContainerPath(string? repositoryRoot, string requestedContainer)
    {
        if (Path.IsPathRooted(requestedContainer))
        {
            return requestedContainer;
        }

        return string.IsNullOrWhiteSpace(repositoryRoot)
            ? requestedContainer
            : Path.Combine(repositoryRoot, requestedContainer);
    }

    private IGrfAssetLookupService CreateGrfAssetLookupService() =>
        new IndexedGrfAssetLookupService(
            new JsonGrfContainerIndexStore(_workspaceRoot),
            new GrfAssemblyAssetLookupService());

    private static RepositoryDiscoveryService CreateDiscoveryService() =>
        new(
            new RathenaScanner(),
            new PatchScanner(),
            new GrfRepositoryScanner(),
            new GrfEditorProbe());

    private static ItemDefinitionInput ToItemInput(ItemDryRunRequest request) =>
        new(
            request.Id,
            request.AegisName,
            request.DisplayName,
            string.IsNullOrWhiteSpace(request.ResourceName) ? request.AegisName : request.ResourceName,
            request.Type,
            request.Buy,
            request.Sell,
            request.Weight,
            request.Slots,
            request.Script,
            request.IdentifiedDescriptionLines ?? [],
            request.UnidentifiedDisplayName,
            request.UnidentifiedResourceName,
            request.UnidentifiedDescriptionLines ?? []);

    private static EquipmentDefinitionInput ToEquipmentInput(EquipmentDryRunRequest request) =>
        new(
            new ItemDefinitionInput(
                request.Id,
                request.AegisName,
                request.DisplayName,
                string.IsNullOrWhiteSpace(request.ResourceName) ? request.AegisName : request.ResourceName,
                request.Type,
                request.Buy,
                request.Sell,
                request.Weight,
                request.Slots,
                request.Script,
                request.IdentifiedDescriptionLines ?? [],
                request.UnidentifiedDisplayName,
                request.UnidentifiedResourceName,
                request.UnidentifiedDescriptionLines ?? []),
            request.EquipLocations ?? [],
            request.VisualCategory,
            request.ViewId,
            request.ClientSymbolName,
            request.ClientSpriteName,
            request.AllowedJobs ?? [],
            request.Gender,
            request.EquipLevelMin,
            request.EquipLevelMax,
            request.ArmorLevel,
            request.WeaponLevel,
            request.Defense,
            request.Refineable,
            request.EquipScript,
            request.UnEquipScript,
            request.WeaponBaseType,
            request.WeaponHitSound);

    private static NpcDefinitionInput ToNpcInput(NpcDryRunRequest request) =>
        new(
            request.Name,
            request.MapName,
            request.X,
            request.Y,
            request.Direction,
            request.Sprite,
            request.ScriptBody,
            request.FileSlug,
            request.ClientSymbolName,
            request.ClientIdentityId);

    private static MonsterDefinitionInput ToMonsterInput(MonsterDryRunRequest request)
    {
        var primarySpawn = request.Spawn is null
            ? new MonsterSpawnDefinition(0, 0, 0, 0, null, request.MapName, request.Amount, request.RespawnMilliseconds)
            : ToMonsterSpawn(request.Spawn, request.MapName, request.Amount, request.RespawnMilliseconds);

        return new MonsterDefinitionInput(
            request.Id,
            request.AegisName,
            request.DisplayName,
            request.MapName,
            request.Level,
            request.Hp,
            request.Amount,
            request.RespawnMilliseconds,
            request.SpriteOverride,
            request.FileSlug,
            primarySpawn,
            request.Skill is null ? null : ToMonsterSkill(request.Skill),
            request.Drops?.Select(ToMonsterDrop).ToArray(),
            request.Skills?.Select(ToMonsterSkill).ToArray(),
            request.Spawns?.Select(spawn => ToMonsterSpawn(spawn, request.MapName, request.Amount, request.RespawnMilliseconds)).ToArray(),
            request.AllowFutureDropReferences);
    }

    private static MonsterDropDefinition ToMonsterDrop(MonsterDropRequest request) =>
        new(request.ItemId, request.ItemAegisName, request.Chance, request.Quantity, request.IsMvp, request.Kind);

    private static MonsterSkillDefinition ToMonsterSkill(MonsterSkillRequest request) =>
        new(
            request.SkillId,
            request.SkillLevel,
            request.State,
            request.Rate,
            request.CastTimeMilliseconds,
            request.DelayMilliseconds,
            request.Cancelable,
            request.Target,
            request.ConditionType,
            request.ConditionValue,
            request.Value1,
            request.Value2,
            request.Value3,
            request.Value4,
            request.Value5,
            request.Emotion,
            request.Chat,
            request.Anchor,
            request.UnsupportedFields);

    private static MonsterSpawnDefinition ToMonsterSpawn(MonsterSpawnRequest request, string defaultMap, int defaultAmount, int defaultRespawn) =>
        new(
            request.X,
            request.Y,
            request.AreaX,
            request.AreaY,
            request.Label,
            string.IsNullOrWhiteSpace(request.MapName) ? defaultMap : request.MapName,
            request.Amount ?? defaultAmount,
            request.RespawnMilliseconds ?? defaultRespawn,
            request.EventLabel,
            request.Randomize);

    private static MapDeploymentInput ToMapInput(MapDryRunRequest request) =>
        new(
            request.MapName,
            string.IsNullOrWhiteSpace(request.RswResourceName) ? request.MapName : request.RswResourceName,
            string.IsNullOrWhiteSpace(request.GndResourceName) ? request.MapName : request.GndResourceName,
            string.IsNullOrWhiteSpace(request.GatResourceName) ? request.MapName : request.GatResourceName,
            request.FileSlug);
}
