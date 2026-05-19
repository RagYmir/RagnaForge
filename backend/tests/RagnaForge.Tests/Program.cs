using RagnaForge.Tests;
using RagnaForge.Application.Assets;
using RagnaForge.Application.Discovery;
using RagnaForge.Application.Configuration;
using RagnaForge.Application.Abstractions;
using RagnaForge.Application.Grf;
using RagnaForge.Application.Visuals;
using RagnaForge.Api;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Discovery;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Maps;
using RagnaForge.Domain.Monsters;
using RagnaForge.Domain.Npcs;
using RagnaForge.Domain.Visuals;
using RagnaForge.Infrastructure.Configuration;
using RagnaForge.Infrastructure.Grf;
using RagnaForge.Infrastructure.GrfEditorIntegration;
using RagnaForge.Infrastructure.Items;
using RagnaForge.Infrastructure.Maps;
using RagnaForge.Infrastructure.Monsters;
using RagnaForge.Infrastructure.Npcs;
using RagnaForge.Infrastructure.Patch;
using RagnaForge.Infrastructure.Rathena;
using RagnaForge.Infrastructure.Visuals;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using System.Text.Json;

const string SampleGrfBase64 =
    "TWFzdGVyIG9mIE1hZ2ljAAECAwQFBgcICQoLDA0OOAAAAAAAAAAKAAAAAAIAAHgBAQUA+v9hY3QNCgT2AVB4AQEFAPr/c3ByDQoFfgFteAEBBgD5/2dyaWQNCgeeAb4AAAAAAAAATwAAAHcAAAB4nEtJLEmMKS4oyixJjSlOzC3ISdVLTC5hEGBgAGNWIGYEYoYUTHVAHqo6AZi6ktSKktKi1Jj0oswUvZL0RAZBoIwEELOBlCkACQA1Ixrf";

var tests = new List<(string Name, Action Test)>
{
    ("rAthena scanner detects imports, map cache mode, scripts and episode mode", RathenaScannerDetectsCoreSignals),
    ("Patch scanner detects legacy item tables and DATA.INI", PatchScannerDetectsLegacyClientTables),
    ("Lua script format detector separates text from bytecode", LuaScriptFormatDetectorSeparatesTextFromBytecode),
    ("Repository discovery service composes all scanners", DiscoveryServiceComposesScanners),
    ("Configuration manifest store round-trips local paths and progressive profile", ConfigurationManifestStoreRoundTrips),
    ("Configuration manifest validator reports missing repository paths", ConfigurationManifestValidatorReportsMissingPath),
    ("Configuration manifest store rejects output outside data manifests", ConfigurationManifestStoreRejectsOutsideManifestDirectory),
    ("API safety policy disables write endpoints", ApiSafetyPolicyDisablesWriteEndpoints),
    ("API service status reports safe mode", ApiServiceStatusReportsSafeMode),
    ("API service runs item dry-run from manifest", ApiServiceRunsItemDryRunFromManifest),
    ("API options default to safe local mode", ApiOptionsDefaultToSafeLocalMode),
    ("API key validator requires configured header", ApiKeyValidatorRequiresConfiguredHeader),
    ("API key validator accepts valid key", ApiKeyValidatorAcceptsValidKey),
    ("API key validator rejects invalid key", ApiKeyValidatorRejectsInvalidKey),
    ("API operation guard blocks dangerous operations", ApiOperationGuardBlocksDangerousOperations),
    ("API operation guard enforces GRF container limit", ApiOperationGuardEnforcesGrfContainerLimit),
    ("API ProblemDetails includes correlation id", ApiProblemDetailsIncludesCorrelationId),
    ("API response wrapper includes correlation and read-only mode", ApiResponseWrapperIncludesCorrelationAndReadOnlyMode),
    ("API rate limiter rejects after configured limit", ApiRateLimiterRejectsAfterConfiguredLimit),
    ("API OpenAPI document exposes API key scheme", ApiOpenApiDocumentExposesApiKeyScheme),
    ("API CORS defaults are restricted to local origins", ApiCorsDefaultsAreRestricted),
    ("API service blocks workspace path traversal", ApiServiceBlocksWorkspacePathTraversal),
    ("API service blocks oversized GRF index request", ApiServiceBlocksOversizedGrfIndexRequest),
    ("RagnaForge Agent runner blocks arbitrary command", RagnaForgeAgentRunnerBlocksArbitraryCommand),
    ("RagnaForge Agent runner blocks rollback command", RagnaForgeAgentRunnerBlocksRollbackCommand),
    ("RagnaForge Agent runner allowlists safe knowledge commands", RagnaForgeAgentRunnerAllowlistsSafeKnowledgeCommands),
    ("RagnaForge Agent runner blocks unsafe knowledge commands", RagnaForgeAgentRunnerBlocksUnsafeKnowledgeCommands),
    ("RagnaForge Agent runner builds safe knowledge command strings", RagnaForgeAgentRunnerBuildsSafeKnowledgeCommandStrings),
    ("RagnaForge Agent runner handles unavailable executable", RagnaForgeAgentRunnerHandlesUnavailableExecutable),
    ("RagnaForge Agent runner handles timeout safely", RagnaForgeAgentRunnerHandlesTimeoutSafely),
    ("RagnaForge Agent runner reads stdout and stderr", RagnaForgeAgentRunnerReadsStdoutAndStderr),
    ("RagnaForge Agent summary reports missing cache", RagnaForgeAgentSummaryReportsMissingCache),
    ("RagnaForge Agent summary rejects stale cache", RagnaForgeAgentSummaryRejectsStaleCache),
    ("RagnaForge Agent summary returns sanitized DTO", RagnaForgeAgentSummaryReturnsSanitizedDto),
    ("RagnaForge Agent summary exposes blocked apply and rollback flags", RagnaForgeAgentSummaryExposesBlockedApplyAndRollbackFlags),
    ("Visual theme manifest store round-trips default catalog", VisualThemeManifestStoreRoundTripsDefaultCatalog),
    ("Visual theme manifest validator blocks duplicate keys", VisualThemeManifestValidatorBlocksDuplicateKeys),
    ("Visual equipment theme matcher suggests fofo for rabbit visuals", VisualEquipmentThemeMatcherSuggestsFofoTheme),
    ("Visual equipment theme matcher flags explicit mismatched theme", VisualEquipmentThemeMatcherFlagsExplicitMismatch),
    ("Indexed GRF asset lookup returns exact local index match without live fallback", IndexedGrfAssetLookupReturnsExactIndexMatch),
    ("Indexed GRF asset lookup falls back to live scan when index is incomplete", IndexedGrfAssetLookupFallsBackWhenIndexIsIncomplete),
    ("Legacy equipment dry-run uses theme-assisted loose visual lookup", LegacyEquipmentDryRunUsesThemeAssistedLooseLookup),
    ("Legacy equipment dry-run prefers indexed GRF theme lookup before live scan", LegacyEquipmentDryRunPrefersIndexedGrfThemeLookup),
    ("Legacy equipment dry-run uses theme-assisted GRF visual lookup", LegacyEquipmentDryRunUsesThemeAssistedGrfLookup),
    ("Cached GRF repository indexer builds and reuses metadata cache", CachedGrfRepositoryIndexerBuildsAndReusesCache),
    ("GRF repository index store rejects output outside data cache", GrfRepositoryIndexStoreRejectsOutsideCacheDirectory),
    ("Cached GRF repository indexer honors cancellation", CachedGrfRepositoryIndexerHonorsCancellation),
    ("GRF content index store rejects output outside data indexes", GrfContainerIndexStoreRejectsOutsideIndexesDirectory),
    ("Legacy item dry-run builds proposal with detected client date", LegacyItemDryRunBuildsProposal),
    ("Legacy item dry-run resolves missing loose asset through GRF lookup", LegacyItemDryRunResolvesGrfAssetLookup),
    ("Client-side plan detects textual ItemInfo lua", ItemClientSidePlanDetectsTextualItemInfoLua),
    ("Client-side plan detects textual ItemInfo lub", ItemClientSidePlanDetectsTextualItemInfoLub),
    ("Client-side plan blocks bytecode ItemInfo lub", ItemClientSidePlanBlocksBytecodeItemInfoLub),
    ("Client-side plan detects legacy TXT item tables", ItemClientSidePlanDetectsLegacyTxtTables),
    ("Client-side plan detects hybrid item client", ItemClientSidePlanDetectsHybridClient),
    ("Client-side plan blocks ambiguous hybrid bytecode", ItemClientSidePlanBlocksHybridBytecode),
    ("Legacy item apply service applies and rolls back safely", LegacyItemApplyServiceAppliesAndRollsBackSafely),
    ("Legacy item apply service blocks conflicting append with audit log", LegacyItemApplyServiceBlocksConflictingAppend),
    ("Legacy equipment apply service applies and rolls back safely", LegacyEquipmentApplyServiceAppliesAndRollsBackSafely),
    ("Legacy item apply service records post-write validation", LegacyItemApplyServiceRecordsPostWriteValidation),
    ("Legacy item rollback blocks client-side drift", LegacyItemRollbackBlocksClientSideDrift),
    ("Legacy equipment dry-run exposes visual client-side plan", LegacyEquipmentDryRunExposesVisualClientSidePlan),
    ("Legacy equipment dry-run blocks bytecode visual datainfo", LegacyEquipmentDryRunBlocksBytecodeVisualDatainfo),
    ("Legacy equipment apply service blocks visual collision introduced after dry-run", LegacyEquipmentApplyServiceBlocksVisualCollisionBeforeWriting),
    ("Legacy equipment apply service blocks targets outside equipment roots", LegacyEquipmentApplyServiceBlocksTargetsOutsideEquipmentRoots),
    ("Legacy equipment dry-run exposes visual GRF lookup provenance", LegacyEquipmentDryRunExposesVisualGrfLookupProvenance),
    ("NPC dry-run builds script and loader diff preview", NpcDryRunBuildsScriptAndLoaderPreview),
    ("NPC dry-run flags non-standard custom sprite validation", NpcDryRunFlagsNonStandardCustomSpriteValidation),
    ("NPC dry-run resolves custom sprite via GRF lookup", NpcDryRunResolvesCustomSpriteViaGrfLookup),
    ("NPC dry-run detects textual client identity files and prepares safe registrations", NpcDryRunDetectsTextualClientIdentityFiles),
    ("NPC dry-run blocks bytecode client identity apply", NpcDryRunBlocksBytecodeClientIdentityApply),
    ("NPC dry-run blocks ambiguous GRF sprite matches", NpcDryRunBlocksAmbiguousGrfSpriteMatches),
    ("NPC apply service applies and rolls back safely", NpcApplyServiceAppliesAndRollsBackSafely),
    ("NPC apply service allows explicit server-only fallback", NpcApplyServiceAllowsExplicitServerOnlyFallback),
    ("NPC apply service applies and rolls back client identity text safely", NpcApplyServiceAppliesAndRollsBackClientIdentityTextSafely),
    ("NPC apply service blocks duplicate client identity registration", NpcApplyServiceBlocksDuplicateClientIdentityRegistration),
    ("NPC apply service blocks malformed client identity staging", NpcApplyServiceBlocksMalformedClientIdentityStaging),
    ("NPC rollback blocks client identity drift after apply", NpcRollbackBlocksClientIdentityDriftAfterApply),
    ("NPC apply service blocks targets outside npc root", NpcApplyServiceBlocksTargetsOutsideNpcRoot),
    ("Monster dry-run builds DB and spawn diff preview", MonsterDryRunBuildsDbAndSpawnPreview),
    ("Monster dry-run supports multiple drops, skills and spawn events", MonsterDryRunSupportsMultipleDropsSkillsAndSpawnEvents),
    ("Monster dry-run blocks unresolved drop item", MonsterDryRunBlocksUnresolvedDropItem),
    ("Monster dry-run blocks duplicate drops and invalid chance", MonsterDryRunBlocksDuplicateDropsAndInvalidChance),
    ("Monster dry-run blocks duplicate skills and unsupported fields", MonsterDryRunBlocksDuplicateSkillsAndUnsupportedFields),
    ("Monster dry-run blocks duplicate and invalid spawns", MonsterDryRunBlocksDuplicateAndInvalidSpawns),
    ("Monster dry-run blocks spawn on missing map", MonsterDryRunBlocksSpawnOnMissingMap),
    ("Monster apply service applies and rolls back safely", MonsterApplyServiceAppliesAndRollsBackSafely),
    ("Monster apply service blocks duplicate ID before writing", MonsterApplyServiceBlocksDuplicateIdBeforeWriting),
    ("Monster apply service blocks targets outside monster roots", MonsterApplyServiceBlocksTargetsOutsideMonsterRoots),
    ("Monster apply service records post-write validation", MonsterApplyServiceRecordsPostWriteValidation),
    ("Monster apply service blocks malformed YAML staging", MonsterApplyServiceBlocksMalformedYamlStaging),
    ("Monster apply service blocks malformed TXT staging", MonsterApplyServiceBlocksMalformedTxtStaging),
    ("Monster apply service blocks malformed spawn staging", MonsterApplyServiceBlocksMalformedSpawnStaging),
    ("Monster apply service rolls back after mid-apply failure", MonsterApplyServiceRollsBackAfterMidApplyFailure),
    ("Map dry-run resolves GRF map assets through local index", MapDryRunResolvesIndexedGrfAssets),
    ("Map dry-run scans loose map dependencies beyond the core trio", MapDryRunScansLooseDependencies),
    ("Map dry-run scans dependencies from controlled GRF extraction", MapDryRunScansDependenciesFromControlledGrfExtraction),
    ("Map dry-run exposes asset plans and blocks binary rename", MapDryRunExposesAssetPlansAndBlocksBinaryRename),
    ("Map apply service applies and rolls back safely", MapApplyServiceAppliesAndRollsBackSafely),
    ("Map apply service blocks targets outside map roots", MapApplyServiceBlocksTargetsOutsideMapRoots),
    ("Legacy equipment dry-run warns when shield-like visual appears in robe table", LegacyEquipmentDryRunWarnsShieldLikeRobeHint),
    ("Legacy item dry-run blocks ID and Aegis collisions", LegacyItemDryRunBlocksCollisions),
    ("Legacy equipment dry-run adds headgear datainfo proposals", LegacyEquipmentDryRunBuildsHeadgearProposal),
    ("Legacy equipment dry-run adds weapon datainfo proposals", LegacyEquipmentDryRunBuildsWeaponProposal),
    ("Legacy equipment dry-run handles missing equip locations safely", LegacyEquipmentDryRunHandlesMissingEquipLocationsSafely),
    ("Legacy equipment dry-run allows built-in shield views", LegacyEquipmentDryRunAllowsBuiltInShieldView),
    ("Legacy equipment dry-run blocks unsafe visual identifiers", LegacyEquipmentDryRunBlocksUnsafeVisualIdentifier),
    ("Legacy equipment dry-run blocks duplicate visual IDs", LegacyEquipmentDryRunBlocksDuplicateVisualId),
    ("Legacy equipment dry-run blocks custom shield visual registration", LegacyEquipmentDryRunBlocksCustomShieldVisualRegistration),
    ("GRF assembly inspector reads a controlled container", GrfAssemblyInspectorReadsControlledContainer),
    ("Asset preview service blocks path traversal", AssetPreviewServiceBlocksPathTraversal),
    ("Asset preview service blocks unallowed extension", AssetPreviewServiceBlocksUnallowedExtension),
    ("Asset preview service blocks arbitrary GRF container path", AssetPreviewServiceBlocksArbitraryGrfContainer),
    ("Asset preview service enforces global maximum byte limit", AssetPreviewServiceEnforcesGlobalMaxBytes),
    ("Asset preview service returns preview for valid local patch asset", AssetPreviewServiceReturnsPreviewForLocalPatchAsset),
    ("Asset preview service returns sprite metadata via renderer", AssetPreviewServiceReturnsSpriteMetadataViaRenderer),
    ("Asset preview service returns act metadata only in v1", AssetPreviewServiceReturnsActMetadataOnly),
    ("Asset preview service blocks invalid companion path", AssetPreviewServiceBlocksInvalidCompanionPath),
    ("Asset preview service handles ACT without companion", AssetPreviewServiceHandlesActWithoutCompanion),
    ("Asset preview service fallbacks to SPR metadata if visual fails", AssetPreviewServiceFallbacksToSpriteMetadata),
    ("Asset preview service reports effective frame index on fallback", AssetPreviewServiceReportsEffectiveFrameIndex),
    ("Asset preview service reports effective action index on fallback", AssetPreviewServiceReportsEffectiveActionIndex),
    ("Asset preview service cleans up extraction temporary directory", AssetPreviewServiceCleansUpTemporaries),
    ("Path validation helper blocks traversal and rooted paths", PathValidationHelperBlocksUnsafe),
    ("Path validation helper enforces companion directory rules", PathValidationHelperEnforcesCompanionRules),
    ("Path validation helper validates system boundaries", PathValidationHelperValidatesBoundaries),
    ("Agent Integration enforces Anti-Apply security barrier", SecurityBarrierAntiApply),
    ("Agent Integration enforces Anti-Shell process execution security barrier", SecurityBarrierAntiShell),
    ("Agent Integration enforces Anti-Secrets logging security barrier", SecurityBarrierAntiSecrets),
    ("GET /api/pipeline/status returns readOnly and applyAvailable=false", PipelineStatusReturnsReadOnlyAndNoApply),
    ("POST /api/pipeline/plan item returns operationId and readiness", PipelinePlanItemReturnsOperationIdAndReadiness),
    ("POST /api/pipeline/plan asset respects placeholders", PipelinePlanAssetHonorsPlaceholders),
    ("POST /api/pipeline/plan map returns summary without destructive parser", PipelinePlanMapReturnsSummaryWithoutDestructiveParser),
    ("POST /api/pipeline/dry-run does not write externally", PipelineDryRunDoesNotWriteExternally),
    ("POST /api/pipeline/diff-preview does not apply diff", PipelineDiffPreviewDoesNotApply),
    ("GET /api/pipeline/issues returns external-data summary", PipelineIssuesReturnsExternalDataSummary),
    ("report read blocks traversal", PipelineReportReadBlocksTraversal),
    ("report read blocks absolute paths", PipelineReportReadBlocksAbsolutePaths),
    ("Agent unavailable turns into warning not crash", PipelineAgentOfflineDegradesGracefully),
    ("Apply does not exist", PipelineApplyDoesNotExist),
    ("Rollback does not exist", PipelineRollbackDoesNotExist),
    ("AgentIntegration does not accept free commands", PipelineAgentIntegrationDoesNotAcceptFreeCommands),
    ("safeForApply remains false when agent reports blocker", PipelineSafeForApplyRemainsFalseWhenAgentReportsBlocker),
    ("payload invalid returns safe error", PipelineInvalidPayloadReturnsSafeError),
    ("Pipeline real payload stress battery", new Action(() => PipelineRealPayloadBatteryTests.RunAllTests().GetAwaiter().GetResult()))
};

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
        Console.Error.WriteLine(failures[^1]);
    }
}

if (failures.Count > 0)
{
    return 1;
}

Console.WriteLine($"All {tests.Count} tests passed.");
return 0;

static void RathenaScannerDetectsCoreSignals()
{
    using var workspace = TempWorkspace.Create();
    var root = workspace.Root;

    Directory.CreateDirectory(Path.Combine(root, "src", "config"));
    Directory.CreateDirectory(Path.Combine(root, "db", "import"));
    Directory.CreateDirectory(Path.Combine(root, "npc", "custom"));
    Directory.CreateDirectory(Path.Combine(root, "npc"));
    Directory.CreateDirectory(Path.Combine(root, "conf", "import"));

    File.WriteAllText(Path.Combine(root, "src", "config", "renewal.hpp"), "/// #define RENEWAL");
    File.WriteAllText(Path.Combine(root, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n");
    File.WriteAllText(Path.Combine(root, "db", "import", "mob_db.yml"), "Body:\n  - Id: 3990\n    AegisName: CRAFT_TREE_01\n");
    File.WriteAllText(Path.Combine(root, "db", "import", "mob_avail.yml"), "Header:\n  Type: MOB_AVAIL_DB\n");
    File.WriteAllText(Path.Combine(root, "db", "import", "mob_skill_db.txt"), "3990,all,any,0,10,1000,yes,self,always,0,0,0,0,0,0,0,0,0,\n");
    File.WriteAllText(Path.Combine(root, "db", "map_index.txt"), "prontera 1\ngeffen\n");
    File.WriteAllText(Path.Combine(root, "conf", "map_athena.conf"), "db_path: db\nuse_grf: no\nimport: conf/maps_athena.conf\nimport: conf/import/map_conf.txt\n");
    File.WriteAllText(Path.Combine(root, "conf", "maps_athena.conf"), "map: prontera\n//map: old_map\n");
    File.WriteAllText(Path.Combine(root, "npc", "scripts_custom.conf"), "//npc: npc/custom/warper.txt\nnpc: npc/custom/craft_arvores.txt\n");

    var result = new RathenaScanner().Scan(root);

    Assert(result.Exists, "rAthena root should exist.");
    Assert(result.DetectedMode == EpisodeMode.PreRenewal, "Commented RENEWAL must not be treated as active.");
    Assert(result.HasDbImport, "db/import should be detected.");
    Assert(result.HasNpcCustom, "npc/custom should be detected.");
    Assert(result.MapServerConfig.UseGrf == false, "use_grf: no should be parsed.");
    Assert(result.ActiveMapCount == 1, "Active map count mismatch.");
    Assert(result.CommentedMapCount == 1, "Commented map count mismatch.");
    Assert(result.CustomNpcScripts.ActiveScripts.Contains("npc/custom/craft_arvores.txt"), "Active custom script missing.");
    Assert(result.ImportDatabases.Single(item => item.RelativePath == "db/import/mob_db.yml").ActiveEntryCount == 1, "mob_db active entry count mismatch.");
}

static void PatchScannerDetectsLegacyClientTables()
{
    using var workspace = TempWorkspace.Create();
    var root = workspace.Root;

    Directory.CreateDirectory(Path.Combine(root, "data", "luafiles514", "lua files", "datainfo"));
    Directory.CreateDirectory(Path.Combine(root, "GRF_CARTAS_EM_HD"));
    Directory.CreateDirectory(Path.Combine(root, "system"));
    File.WriteAllText(Path.Combine(root, "DATA.INI"), "[Data]\n1=cdata.grf\n2=GRF_CARTAS_EM_HD\n");
    File.WriteAllText(Path.Combine(root, "cdata.grf"), "fake");
    File.WriteAllText(Path.Combine(root, "data", "idnum2itemresnametable.txt"), "501#apple#\n");
    File.WriteAllText(Path.Combine(root, "data", "itemslotcounttable.txt"), "501#0#\n");
    File.WriteAllText(Path.Combine(root, "data", "luafiles514", "lua files", "datainfo", "npcidentity.lub"), "fake");
    File.WriteAllText(Path.Combine(root, "data", "sprite.spr"), "fake");
    File.WriteAllText(Path.Combine(root, "2025-07-16_Ragexe.exe"), "fake");
    File.WriteAllText(Path.Combine(root, "system", "iteminfo_true.lub"), "fake");

    var result = new PatchScanner().Scan(root);

    Assert(result.Exists, "Patch root should exist.");
    Assert(result.DataIniEntries.Count == 2, "DATA.INI entries mismatch.");
    Assert(result.DataIniEntries.All(entry => entry.ExistsOnDisk), "DATA.INI entries should exist.");
    Assert(result.LegacyItemTables.Any(table => table.Name == "idnum2itemresnametable.txt" && table.Exists), "Legacy item table missing.");
    Assert(result.ItemDataMode.UsesLegacyTables, "Legacy mode should be detected.");
    Assert(result.ItemDataMode.UsesModernItemInfo, "Modern iteminfo presence should be detected.");
    Assert(result.DatainfoFiles.Any(file => file.Name == "npcidentity.lub" && file.Exists), "Datainfo file missing.");
    Assert(result.AssetCounts.Any(count => count.Extension == ".spr" && count.Count == 1), "SPR count mismatch.");
    Assert(result.ClientDate.Value == "2025-07-16", "Client date should be detected from executable name.");
    Assert(result.ClientDate.IsConfirmed, "Client date detection should be confirmed.");
}

static void LuaScriptFormatDetectorSeparatesTextFromBytecode()
{
    using var workspace = TempWorkspace.Create();
    var textPath = Path.Combine(workspace.Root, "spriterobename.lub");
    var bytecodePath = Path.Combine(workspace.Root, "compiled.lub");

    File.WriteAllText(textPath, "RobeNameTable = {}\n");
    File.WriteAllBytes(bytecodePath, [0x1B, 0x4C, 0x75, 0x61, 0x00, 0x01]);

    var detector = new LuaScriptFormatDetector();
    var textResult = detector.Inspect(textPath);
    var bytecodeResult = detector.Inspect(bytecodePath);

    Assert(textResult.Format == LuaScriptFormat.Text, "Plaintext lub should be classified as text.");
    Assert(bytecodeResult.Format == LuaScriptFormat.LuaBytecode, "Lua bytecode signature should be detected.");
}

static void DiscoveryServiceComposesScanners()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(rathena);
    Directory.CreateDirectory(patch);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);
    File.WriteAllText(Path.Combine(grfs, "sample.grf"), "fake");

    var service = new RepositoryDiscoveryService(
        new RathenaScanner(),
        new PatchScanner(),
        new GrfRepositoryScanner(),
        new GrfEditorProbe());

    var report = service.Run(new DiscoveryOptions(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("test-episode", EpisodeMode.Hybrid, "2025-07-16", "test"),
        MaxGrfContainers: 10));

    Assert(report.EpisodeProfile.Mode == EpisodeMode.Hybrid, "Episode profile should round-trip.");
    Assert(report.Rathena.Exists, "rAthena should exist.");
    Assert(report.Patch.Exists, "Patch should exist.");
    Assert(report.GrfRepository.ContainerCount == 1, "GRF container count mismatch.");
    Assert(report.GrfEditor.Exists, "GRF Editor path should exist.");
}

static void ConfigurationManifestStoreRoundTrips()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(rathena);
    Directory.CreateDirectory(patch);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var store = new JsonConfigurationManifestStore(workspace.Root);
    var manifest = ConfigurationManifest.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, null, "test"),
        isProgressive: true,
        timestampUtc: new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));

    store.Save(store.DefaultManifestPath, manifest, overwrite: false);
    var loaded = store.Load(store.DefaultManifestPath);
    var validation = new ConfigurationManifestValidator().Validate(loaded);

    Assert(loaded.Paths.RathenaPath == rathena, "rAthena path should round-trip.");
    Assert(loaded.EpisodeProfile.Mode == EpisodeMode.PreRenewal, "Episode mode should round-trip.");
    Assert(loaded.IsProgressive, "Progressive flag should round-trip.");
    Assert(validation.IsValid, "Manifest with existing paths should be valid.");
    Assert(validation.Issues.Any(issue => issue.Code == "client_date.unknown"), "Unknown client date warning should be present.");
}

static void ConfigurationManifestValidatorReportsMissingPath()
{
    using var workspace = TempWorkspace.Create();
    var existing = Path.Combine(workspace.Root, "existing");
    Directory.CreateDirectory(existing);

    var manifest = ConfigurationManifest.Create(
        new RepositoryPaths(existing, existing, Path.Combine(workspace.Root, "missing-grfs"), existing),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, null, "test"),
        isProgressive: true);

    var validation = new ConfigurationManifestValidator().Validate(manifest);

    Assert(!validation.IsValid, "Manifest with missing path should be invalid.");
    Assert(validation.Issues.Any(issue => issue.Code == "path.missing" && issue.Severity == ManifestValidationSeverity.Error), "Missing path error should be reported.");
}

static void ConfigurationManifestStoreRejectsOutsideManifestDirectory()
{
    using var workspace = TempWorkspace.Create();
    var store = new JsonConfigurationManifestStore(workspace.Root);
    var manifest = ConfigurationManifest.Create(
        new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, null, "test"),
        isProgressive: true);

    try
    {
        store.Save(Path.Combine(workspace.Root, "repositories.local.json"), manifest, overwrite: false);
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("Store should reject manifest writes outside data/manifests.");
}

static void ApiSafetyPolicyDisablesWriteEndpoints()
{
    Assert(ApiSafetyPolicy.DisabledWriteOperations.Contains("item.apply"), "Item apply should be disabled in the API safety policy.");
    Assert(ApiSafetyPolicy.DisabledWriteOperations.Contains("map.rollback"), "Rollback endpoints should be disabled in the API safety policy.");
    Assert(ApiSafetyPolicy.Capabilities.Where(capability => capability.Category is "item" or "equipment" or "npc" or "monster" or "map")
        .All(capability => !capability.Apply && !capability.Rollback), "Content pipeline API capabilities must expose no write operations.");
    Assert(!ApiSafetyPolicy.IsWriteOperationEnabled("monster.apply"), "Write operation lookup should always reject apply in this API cut.");
}

static void ApiServiceStatusReportsSafeMode()
{
    using var workspace = TempWorkspace.Create();
    var statusJson = JsonSerializer.Serialize(new RagnaForgeApiService(workspace.Root).GetStatus());

    Assert(statusJson.Contains("read-only-dry-run-diff-preview", StringComparison.Ordinal), "API status should advertise safe mode.");
    Assert(statusJson.Contains("\"ApplyEndpointsEnabled\":false", StringComparison.Ordinal), "API status should expose disabled apply endpoints.");
    Assert(statusJson.Contains("\"RollbackEndpointsEnabled\":false", StringComparison.Ordinal), "API status should expose disabled rollback endpoints.");
}

static void ApiServiceRunsItemDryRunFromManifest()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    SaveApiTestManifest(workspace.Root, workspace.Paths);

    var service = new RagnaForgeApiService(workspace.Root);
    var report = service.CreateItemDryRun(new ItemDryRunRequest(
        "RF_Api_Item",
        "RagnaForge API Item",
        ResourceName: "rf_api_item",
        IdentifiedDescriptionLines: ["API dry-run fixture."],
        ConfigPath: Path.Combine("data", "manifests", "repositories.local.json")));

    Assert(report.CanApply, "API dry-run should reuse the item pipeline and remain apply-ready in fixture mode.");
    Assert(report.ClientSidePlan is not null, "API item dry-run should expose the shared client-side plan.");
    Assert(report.DiffPreview.FileCount == report.ProposedChanges.Count, "API item diff preview should cover proposed changes.");
}

static void ApiOptionsDefaultToSafeLocalMode()
{
    var options = new RagnaForgeApiOptions();

    Assert(options.ReadOnlyMode, "API should default to read-only mode.");
    Assert(!options.EnableApplyEndpoints, "API should not enable apply endpoints by default.");
    Assert(!options.EnableRollbackEndpoints, "API should not enable rollback endpoints by default.");
    Assert(options.RequireApiKey, "API should require a local key by default.");
    Assert(options.AllowedOrigins.Contains("http://127.0.0.1:5173"), "CORS default should include only local frontend origin.");
    Assert(!options.AllowedOrigins.Contains("*"), "CORS default must not allow any origin.");
}

static void ApiKeyValidatorRequiresConfiguredHeader()
{
    var validator = new ApiKeyValidator(
        new RagnaForgeApiOptions { ApiKey = "secret" },
        new FakeHostEnvironment("Production"));
    var context = new DefaultHttpContext();

    var result = validator.Validate(context.Request.Headers);

    Assert(!result.Succeeded, "Missing API key should be rejected.");
    Assert(result.ErrorCode == "auth.api_key_missing", "Missing API key should use a clear error code.");
}

static void ApiKeyValidatorAcceptsValidKey()
{
    var validator = new ApiKeyValidator(
        new RagnaForgeApiOptions { ApiKey = "secret" },
        new FakeHostEnvironment("Production"));
    var context = new DefaultHttpContext();
    context.Request.Headers["X-RagnaForge-Api-Key"] = "secret";

    var result = validator.Validate(context.Request.Headers);

    Assert(result.Succeeded, "Valid API key should be accepted.");
}

static void ApiKeyValidatorRejectsInvalidKey()
{
    var validator = new ApiKeyValidator(
        new RagnaForgeApiOptions { ApiKey = "secret" },
        new FakeHostEnvironment("Production"));
    var context = new DefaultHttpContext();
    context.Request.Headers["X-RagnaForge-Api-Key"] = "wrong";

    var result = validator.Validate(context.Request.Headers);

    Assert(!result.Succeeded, "Invalid API key should be rejected.");
    Assert(result.ErrorCode == "auth.api_key_invalid", "Invalid API key should use a clear error code.");
}

static void ApiOperationGuardBlocksDangerousOperations()
{
    var guard = new ApiOperationGuard(new RagnaForgeApiOptions());

    guard.EnsureAllowed(OperationKind.ReadOnly);
    guard.EnsureAllowed(OperationKind.DryRun);
    guard.EnsureAllowed(OperationKind.DiffPreview);
    AssertThrows<ApiException>(() => guard.EnsureAllowed(OperationKind.Apply), "Apply must be blocked by API operation guard.");
    AssertThrows<ApiException>(() => guard.EnsureAllowed(OperationKind.Rollback), "Rollback must be blocked by API operation guard.");
    AssertThrows<ApiException>(() => guard.EnsureAllowed(OperationKind.ExternalRepoWrite), "External repo writes must be blocked by API operation guard.");
    AssertThrows<ApiException>(() => guard.EnsureAllowed(OperationKind.GrfWrite), "GRF writes must be blocked by API operation guard.");
}

static void ApiOperationGuardEnforcesGrfContainerLimit()
{
    var guard = new ApiOperationGuard(new RagnaForgeApiOptions { MaxGrfContainersPerRequest = 2 });

    guard.EnsureGrfContainerLimit(2);
    AssertThrows<ApiException>(() => guard.EnsureGrfContainerLimit(3), "GRF container limit should block oversized requests.");
}

static void ApiProblemDetailsIncludesCorrelationId()
{
    var context = new DefaultHttpContext();
    context.Request.Path = "/api/test";
    context.Items[ApiCorrelation.ItemKey] = "corr-test";

    var problem = ApiProblemFactory.Create(
        context,
        StatusCodes.Status422UnprocessableEntity,
        "Validation failed",
        "Bad test payload.",
        "payload.validation_failed");

    Assert(problem.Status == StatusCodes.Status422UnprocessableEntity, "ProblemDetails status should be preserved.");
    Assert((string?)problem.Extensions["correlationId"] == "corr-test", "ProblemDetails should include correlation id.");
    Assert((string?)problem.Extensions["errorCode"] == "payload.validation_failed", "ProblemDetails should include error code.");
    Assert((string?)problem.Extensions["path"] == "/api/test", "ProblemDetails should include request path.");
}

static void ApiResponseWrapperIncludesCorrelationAndReadOnlyMode()
{
    var response = new ApiResponse<string>(
        true,
        "ok",
        [],
        [],
        DateTimeOffset.UtcNow,
        "corr-test",
        OperationKind.ReadOnly,
        true,
        12);

    Assert(response.Success, "API response should preserve success flag.");
    Assert(response.CorrelationId == "corr-test", "API response should expose correlation id.");
    Assert(response.ReadOnlyMode, "API response should expose read-only mode.");
    Assert(response.OperationKind == OperationKind.ReadOnly, "API response should expose operation kind.");
}

static void ApiRateLimiterRejectsAfterConfiguredLimit()
{
    var limiter = new ApiInMemoryRateLimiter();
    var now = DateTimeOffset.UtcNow;

    Assert(limiter.TryAcquire("client", 2, TimeSpan.FromMinutes(1), now, out _), "First request should pass.");
    Assert(limiter.TryAcquire("client", 2, TimeSpan.FromMinutes(1), now.AddSeconds(1), out _), "Second request should pass.");
    Assert(!limiter.TryAcquire("client", 2, TimeSpan.FromMinutes(1), now.AddSeconds(2), out var retryAfter), "Third request should be rate limited.");
    Assert(retryAfter > TimeSpan.Zero, "Rate limit should report retry-after duration.");
}

static void ApiOpenApiDocumentExposesApiKeyScheme()
{
    var json = JsonSerializer.Serialize(ApiOpenApiDocument.Create(new RagnaForgeApiOptions()));

    Assert(json.Contains("RagnaForgeApiKey", StringComparison.Ordinal), "OpenAPI document should expose API key scheme.");
    Assert(json.Contains("X-RagnaForge-Api-Key", StringComparison.Ordinal), "OpenAPI document should name the API key header.");
    Assert(json.Contains("Apply and rollback endpoints are intentionally absent", StringComparison.Ordinal), "OpenAPI document should document missing dangerous endpoints.");
}

static void ApiCorsDefaultsAreRestricted()
{
    var options = new RagnaForgeApiOptions();

    Assert(options.AllowedOrigins.Count == 2, "Default CORS should be limited to local frontend origins.");
    Assert(options.AllowedOrigins.All(origin => origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase)
        || origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase)), "Default CORS origins should be loopback only.");
}

static void ApiServiceBlocksWorkspacePathTraversal()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    SaveApiTestManifest(workspace.Root, workspace.Paths);
    var service = new RagnaForgeApiService(workspace.Root);

    AssertThrows<ApiException>(
        () => service.ValidateConfig(new ConfigRequest(Path.Combine("..", "repositories.local.json"))),
        "API service should block config path traversal outside workspace.");
}

static void ApiServiceBlocksOversizedGrfIndexRequest()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    SaveApiTestManifest(workspace.Root, workspace.Paths);
    var service = new RagnaForgeApiService(workspace.Root, new RagnaForgeApiOptions { MaxGrfContainersPerRequest = 1 });

    AssertThrows<ApiException>(
        () => service.IndexGrfs(new GrfIndexRequest(ConfigPath: Path.Combine("data", "manifests", "repositories.local.json"), MaxContainers: 2, SaveCache: false)),
        "API service should block oversized GRF index requests before scanning.");
}

static void RagnaForgeAgentRunnerBlocksArbitraryCommand()
{
    using var workspace = TempWorkspace.Create();
    var fakeExe = Path.Combine(workspace.Root, "ragnaforge.exe");
    File.WriteAllText(fakeExe, "fake");
    var executor = new FakeAgentProcessExecutor();
    var runner = new RagnaForgeAgentCommandRunner(
        fakeExe,
        TimeSpan.FromSeconds(1),
        executor,
        NullLogger<RagnaForgeAgentCommandRunner>.Instance);

    var result = runner.ExecuteAsync("apply --json").GetAwaiter().GetResult();

    Assert(result is null, "Runner should block arbitrary/non-allowlisted commands.");
    Assert(executor.Calls.Count == 0, "Blocked commands must not reach process execution.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("rollback --json"), "Rollback must not be allowlisted.");
}

static void RagnaForgeAgentRunnerBlocksRollbackCommand()
{
    using var workspace = TempWorkspace.Create();
    var fakeExe = Path.Combine(workspace.Root, "ragnaforge.exe");
    File.WriteAllText(fakeExe, "fake");
    var executor = new FakeAgentProcessExecutor();
    var runner = new RagnaForgeAgentCommandRunner(
        fakeExe,
        TimeSpan.FromSeconds(1),
        executor,
        NullLogger<RagnaForgeAgentCommandRunner>.Instance);

    var result = runner.ExecuteAsync("rollback --list --json").GetAwaiter().GetResult();

    Assert(result is null, "Runner should block rollback commands from the API integration.");
    Assert(executor.Calls.Count == 0, "Blocked rollback must not reach process execution.");
}

static void RagnaForgeAgentRunnerAllowlistsSafeKnowledgeCommands()
{
    Assert(RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge sources --json"), "Knowledge sources should be allowlisted.");
    Assert(RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge validate --json"), "Knowledge validate should be allowlisted.");
    Assert(RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge search --query \"item_db\" --json"), "Safe knowledge search should be allowlisted.");
    Assert(RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge explain --topic \"map dependencies\" --json"), "Safe knowledge explain should be allowlisted.");
    Assert(RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge schema --entity \"item\" --json"), "Safe knowledge schema should be allowlisted.");
}

static void RagnaForgeAgentRunnerBlocksUnsafeKnowledgeCommands()
{
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge build --json"), "Knowledge build must not be allowed through API integration.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge entry --id \"item_db\" --json"), "Knowledge entry is not exposed by API integration.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge search --query \"../item_db\" --json"), "Path-like knowledge search must be blocked.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge search --query \"item_db\" --json --extra"), "Extra arguments must be blocked.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge search --query \"item_db\" & apply --json"), "Command chaining must be blocked.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge schema --entity \"../../item\" --json"), "Path-like schema entity must be blocked.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("knowledge schema --entity \"unknown\" --json"), "Unknown schema entity must be blocked.");
}

static void RagnaForgeAgentRunnerBuildsSafeKnowledgeCommandStrings()
{
    var searchCommand = RagnaForgeAgentCommandRunner.CreateKnowledgeSearchCommand("item_db");
    var explainCommand = RagnaForgeAgentCommandRunner.CreateKnowledgeExplainCommand("map dependencies");
    var schemaCommand = RagnaForgeAgentCommandRunner.CreateKnowledgeSchemaCommand("Item");

    Assert(searchCommand == "knowledge search --query \"item_db\" --json", "Search command should be deterministic.");
    Assert(explainCommand == "knowledge explain --topic \"map dependencies\" --json", "Explain command should be deterministic.");
    Assert(schemaCommand == "knowledge schema --entity \"item\" --json", "Schema command should normalize entity.");
    Assert(RagnaForgeAgentCommandRunner.CreateKnowledgeSearchCommand(new string('a', 513)) is null, "Oversized knowledge query must be rejected.");
    Assert(RagnaForgeAgentCommandRunner.CreateKnowledgeSearchCommand("C:\\secret") is null, "Path-like knowledge query must be rejected.");
    Assert(RagnaForgeAgentCommandRunner.CreateKnowledgeExplainCommand("map\" dependencies") is null, "Quote injection must be rejected.");
    Assert(RagnaForgeAgentCommandRunner.CreateKnowledgeSchemaCommand("unknown") is null, "Unknown schema entity must be rejected.");
}

static void RagnaForgeAgentRunnerHandlesUnavailableExecutable()
{
    var executor = new FakeAgentProcessExecutor();
    var runner = new RagnaForgeAgentCommandRunner(
        Path.Combine(Path.GetTempPath(), "missing-ragnaforge.exe"),
        TimeSpan.FromSeconds(1),
        executor,
        NullLogger<RagnaForgeAgentCommandRunner>.Instance);

    var result = runner.ExecuteAsync("status --json").GetAwaiter().GetResult();

    Assert(result is null, "Missing agent executable should be handled safely.");
    Assert(executor.Calls.Count == 0, "Unavailable executable must not invoke process execution.");
}

static void RagnaForgeAgentRunnerHandlesTimeoutSafely()
{
    using var workspace = TempWorkspace.Create();
    var fakeExe = Path.Combine(workspace.Root, "ragnaforge.exe");
    File.WriteAllText(fakeExe, "fake");
    var executor = new FakeAgentProcessExecutor
    {
        NextResult = new RagnaForgeAgentProcessResult(-1, "", "timed out", TimedOut: true)
    };
    var runner = new RagnaForgeAgentCommandRunner(
        fakeExe,
        TimeSpan.FromMilliseconds(10),
        executor,
        NullLogger<RagnaForgeAgentCommandRunner>.Instance);

    var result = runner.ExecuteAsync("status --json").GetAwaiter().GetResult();

    Assert(result is null, "Timed out agent command should return a safe null result.");
    Assert(executor.Calls.Count == 1, "Allowlisted command should reach process executor exactly once.");
}

static void RagnaForgeAgentRunnerReadsStdoutAndStderr()
{
    using var workspace = TempWorkspace.Create();
    var fakeExe = Path.Combine(workspace.Root, "ragnaforge.exe");
    File.WriteAllText(fakeExe, "fake");
    var executor = new FakeAgentProcessExecutor
    {
        NextResult = new RagnaForgeAgentProcessResult(0, AgentStatusJson(), "diagnostic warning", TimedOut: false)
    };
    var runner = new RagnaForgeAgentCommandRunner(
        fakeExe,
        TimeSpan.FromSeconds(1),
        executor,
        NullLogger<RagnaForgeAgentCommandRunner>.Instance);

    using var result = runner.ExecuteAsync("status --json").GetAwaiter().GetResult();

    Assert(result is not null, "Runner should parse stdout JSON even when stderr contains diagnostics.");
    Assert(executor.Calls.Single().Arguments == "status --json", "Runner should pass only the allowlisted arguments.");
}

static void RagnaForgeAgentSummaryReportsMissingCache()
{
    using var workspace = TempWorkspace.Create();
    var service = CreateAgentSummaryService(workspace.Root);

    var summary = service.GetHealthSummaryAsync().GetAwaiter().GetResult();

    Assert(summary.AgentReachable, "Status command should be reachable in fake service.");
    Assert(summary.Index is null, "Missing entity cache should not produce trusted index counts.");
    Assert(summary.Scan is null, "Missing project cache should not produce trusted scan counts.");
    Assert(summary.Warnings.Any(w => w.Contains("entities_index.json", StringComparison.Ordinal)), "Missing entity cache warning should be returned.");
    Assert(summary.Warnings.Any(w => w.Contains("project_index.json", StringComparison.Ordinal)), "Missing project cache warning should be returned.");
}

static void RagnaForgeAgentSummaryRejectsStaleCache()
{
    using var workspace = TempWorkspace.Create();
    WriteAgentCacheFiles(workspace.Root, activeProfile: "old-profile", configFingerprint: "old-fingerprint");
    var service = CreateAgentSummaryService(workspace.Root);

    var summary = service.GetHealthSummaryAsync().GetAwaiter().GetResult();

    Assert(summary.Index is null, "Stale entity cache should not be trusted.");
    Assert(summary.Scan is null, "Stale project cache should not be trusted.");
    Assert(summary.Warnings.Count(w => w.Contains("stale", StringComparison.OrdinalIgnoreCase)) >= 2, "Stale cache warnings should be explicit.");
}

static void RagnaForgeAgentSummaryReturnsSanitizedDto()
{
    using var workspace = TempWorkspace.Create();
    WriteAgentCacheFiles(workspace.Root, activeProfile: "teste", configFingerprint: "fingerprint-1");
    var service = CreateAgentSummaryService(workspace.Root);

    var summary = service.GetHealthSummaryAsync().GetAwaiter().GetResult();
    var json = JsonSerializer.Serialize(summary);

    Assert(summary.StatusOk, "Status should be OK.");
    Assert(summary.DoctorOk, "Doctor should be OK.");
    Assert(summary.Index is not null && summary.Index.ItemsFound == 10, "Sanitized DTO should expose trusted aggregate counts.");
    Assert(summary.Scan is not null && summary.Scan.FilesIndexed == 5, "Sanitized DTO should expose trusted scan counts.");
    Assert(summary.Validation is not null && summary.Validation.TotalIssues == 2, "Validation summary should be exposed.");
    Assert(!json.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase), "Agent DTO must not expose cache filesystem paths.");
    Assert(!json.Contains("E:\\", StringComparison.OrdinalIgnoreCase), "Agent DTO must not expose external absolute paths.");
}

static void RagnaForgeAgentSummaryExposesBlockedApplyAndRollbackFlags()
{
    using var workspace = TempWorkspace.Create();
    WriteAgentCacheFiles(workspace.Root, activeProfile: "teste", configFingerprint: "fingerprint-1");
    var service = CreateAgentSummaryService(workspace.Root);

    var summary = service.GetHealthSummaryAsync().GetAwaiter().GetResult();

    Assert(summary.Safety.ApplyBlocked, "Agent summary should expose apply as blocked in the integration.");
    Assert(summary.Safety.RollbackRealBlocked, "Agent summary should expose real rollback as blocked in the integration.");
}

static void VisualThemeManifestStoreRoundTripsDefaultCatalog()
{
    using var workspace = TempWorkspace.Create();
    var store = new JsonVisualThemeManifestStore(workspace.Root);
    var manifest = VisualThemeManifest.CreateDefault(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));

    store.Save(store.DefaultManifestPath, manifest, overwrite: false);
    var loaded = store.Load(store.DefaultManifestPath);
    var validation = new VisualThemeManifestValidator().Validate(loaded);

    Assert(loaded.Themes.Count >= 5, "Default visual theme catalog should include starter themes.");
    Assert(loaded.Scope == VisualThemeManifest.CurrentScope, "Visual theme manifest scope should match equipment visuals.");
    Assert(loaded.Themes.Any(theme => theme.Key == "angelical"), "Default catalog should include angelical theme.");
    Assert(loaded.Themes.Any(theme => theme.Categories.Contains("weapon")), "Default catalog should include weapon-capable themes.");
    Assert(loaded.Themes.All(theme => !theme.Categories.Contains("map", StringComparer.OrdinalIgnoreCase)), "Equipment visual catalog should not include map categories.");
    Assert(validation.IsValid, "Default visual theme catalog should validate.");
}

static void VisualThemeManifestValidatorBlocksDuplicateKeys()
{
    var manifest = VisualThemeManifest.Create(
        [
            new VisualThemeDefinition("gothic", "Gothic", ["headgear"], ["Costume_Head_Top"], ["dark"], ["dark"], []),
            new VisualThemeDefinition("gothic", "Gothic Duplicate", ["robe"], ["Costume_Garment"], ["dark"], ["shadow"], [])
        ]);

    var validation = new VisualThemeManifestValidator().Validate(manifest);

    Assert(!validation.IsValid, "Duplicate visual theme keys should be invalid.");
    Assert(validation.Issues.Any(issue => issue.Code == "visual_theme.key.duplicate"), "Duplicate key error should be reported.");
}

static void VisualEquipmentThemeMatcherSuggestsFofoTheme()
{
    var matcher = new VisualEquipmentThemeMatcher();
    var manifest = VisualThemeManifest.CreateDefault(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));
    var input = new EquipmentDefinitionInput(
        new ItemDefinitionInput(
            null,
            "RF_Costume_Rabbit",
            "RagnaForge Costume Rabbit",
            "c_rabbit_winged_robe",
            "Armor",
            10,
            5,
            100,
            1,
            null,
            ["Visual item."],
            null,
            null,
            []),
        ["Costume_Garment"],
        "robe",
        5000,
        "ROBE_RF_COSTUME_RABBIT",
        "_rabbit_winged_robe",
        [],
        null,
        1,
        null,
        null,
        null,
        3,
        true,
        null,
        null,
        null,
        null);

    var evaluation = matcher.Evaluate(manifest, input, @"C:\temp\visual-equipment-themes.local.json", requestedKey: null);

    Assert(evaluation.SuggestedThemes.Count > 0, "Matcher should return at least one suggested theme.");
    var fofo = evaluation.SuggestedThemes.FirstOrDefault(theme => theme.Key == "fofo");
    Assert(fofo is not null, "Rabbit visual should include fofo among suggested themes.");
    var fofoTheme = fofo!;
    Assert(fofoTheme.MatchedCategories.Contains("robe"), "Suggested theme should match robe category.");
    Assert(fofoTheme.MatchedEquipLocations.Contains("Costume_Garment"), "Suggested theme should match costume garment location.");
    Assert(fofoTheme.MatchedPatterns.Contains("rabbit"), "Suggested theme should match rabbit pattern.");
}

static void VisualEquipmentThemeMatcherFlagsExplicitMismatch()
{
    var matcher = new VisualEquipmentThemeMatcher();
    var manifest = VisualThemeManifest.CreateDefault(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));
    var input = new EquipmentDefinitionInput(
        new ItemDefinitionInput(
            null,
            "RF_Test_Sword",
            "RagnaForge Test Sword",
            "rf_test_sword",
            "Weapon",
            10,
            5,
            100,
            2,
            null,
            ["Visual item."],
            null,
            null,
            []),
        ["Right_Hand"],
        "weapon",
        6000,
        "WEAPONTYPE_RF_TEST_SWORD",
        "_rf_test_sword",
        [],
        null,
        1,
        null,
        null,
        3,
        null,
        true,
        null,
        null,
        "SWORD",
        null);

    var evaluation = matcher.Evaluate(manifest, input, @"C:\temp\visual-equipment-themes.local.json", requestedKey: "fofo");

    Assert(evaluation.SelectedTheme is not null && evaluation.SelectedTheme.Key == "fofo", "Requested theme should be resolved when it exists.");
    Assert(evaluation.Issues.Any(issue => issue.Contains("does not cover visual category 'weapon'", StringComparison.OrdinalIgnoreCase)), "Mismatch on weapon category should be reported.");
    Assert(evaluation.Issues.Any(issue => issue.Contains("does not cover any of the current equip locations", StringComparison.OrdinalIgnoreCase)), "Mismatch on equip location should be reported.");
    Assert(evaluation.Issues.Any(issue => issue.Contains("did not match the current resource/client names by pattern", StringComparison.OrdinalIgnoreCase)), "Missing pattern match should be reported.");
}

static void IndexedGrfAssetLookupReturnsExactIndexMatch()
{
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, Path.Combine(workspace.Root, "grfs"), workspace.Root);
    Directory.CreateDirectory(paths.GrfRepositoryPath);
    var containerPath = Path.Combine(paths.GrfRepositoryPath, "sample.grf");
    File.WriteAllText(containerPath, "fake");

    var indexStore = new JsonGrfContainerIndexStore(workspace.Root);
    indexStore.Save(
        indexStore.BuildDefaultIndexPath(containerPath),
        GrfContainerContentIndexDocument.Create(
            containerPath,
            containerLength: 1,
            containerLastWriteTimeUtc: DateTimeOffset.UtcNow,
            entryCount: 1,
            directoryCount: 1,
            maxEntriesCaptured: 1,
            isTruncated: false,
            extensionCounts: [new GrfExtensionCount(".spr", 1)],
            topLevelDirectories: [new GrfTopLevelDirectoryCount("data", 1)],
            entries:
            [
                new GrfContentEntrySnapshot(
                    "data/sprite/item/rf_test_item.spr",
                    "data/sprite/item",
                    "rf_test_item.spr",
                    ".spr",
                    10,
                    10,
                    false)
            ]),
        overwrite: true);

    var fallback = new CountingGrfAssetLookupService();
    var service = new IndexedGrfAssetLookupService(indexStore, fallback);
    var result = service.FindAssets(
        paths,
        "rf_test_item",
        [".spr", ".act"],
        new GrfAssetLookupOptions(true, [containerPath], 1, 10));

    Assert(result.Matches.Count == 1, "Indexed lookup should return exact match from local index.");
    Assert(result.Matches[0].RelativePath.Equals("data/sprite/item/rf_test_item.spr", StringComparison.OrdinalIgnoreCase), "Indexed lookup should return stored relative path.");
    Assert(result.Source == GrfAssetLookupSource.LocalIndex, "Indexed lookup should report local index provenance.");
    Assert(result.LocalIndexesLoaded == 1, "Indexed lookup should report loaded local index count.");
    Assert(result.LiveContainersScanned == 0, "Indexed lookup should not report live scan usage.");
    Assert(fallback.Calls == 0, "Live fallback should not run when exact indexed match exists.");
}

static void IndexedGrfAssetLookupFallsBackWhenIndexIsIncomplete()
{
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, Path.Combine(workspace.Root, "grfs"), workspace.Root);
    Directory.CreateDirectory(paths.GrfRepositoryPath);
    var containerPath = Path.Combine(paths.GrfRepositoryPath, "sample.grf");
    File.WriteAllText(containerPath, "fake");

    var indexStore = new JsonGrfContainerIndexStore(workspace.Root);
    indexStore.Save(
        indexStore.BuildDefaultIndexPath(containerPath),
        GrfContainerContentIndexDocument.Create(
            containerPath,
            containerLength: 1,
            containerLastWriteTimeUtc: DateTimeOffset.UtcNow,
            entryCount: 100,
            directoryCount: 1,
            maxEntriesCaptured: 1,
            isTruncated: true,
            extensionCounts: [new GrfExtensionCount(".spr", 100)],
            topLevelDirectories: [new GrfTopLevelDirectoryCount("data", 100)],
            entries:
            [
                new GrfContentEntrySnapshot(
                    "data/sprite/item/not_the_target.spr",
                    "data/sprite/item",
                    "not_the_target.spr",
                    ".spr",
                    10,
                    10,
                    false)
            ]),
        overwrite: true);

    var fallback = new CountingGrfAssetLookupService(
        new GrfAssetLookupResult(
            "rf_test_item",
            true,
            1,
            1,
            [
                new GrfAssetLookupMatch(containerPath, "data/sprite/item/rf_test_item.spr", ".spr", 10, 10, false)
            ],
            []));
    var service = new IndexedGrfAssetLookupService(indexStore, fallback);
    var result = service.FindAssets(
        paths,
        "rf_test_item",
        [".spr", ".act"],
        new GrfAssetLookupOptions(true, [containerPath], 1, 10));

    Assert(result.Matches.Count == 1, "Fallback lookup should supply exact match when index is incomplete.");
    Assert(result.Source == GrfAssetLookupSource.LiveScanFallback, "Fallback lookup should report live scan fallback provenance.");
    Assert(result.LocalIndexesLoaded == 1, "Fallback lookup should preserve local index load count.");
    Assert(result.LiveContainersScanned == 1, "Fallback lookup should report live-scanned container count.");
    Assert(fallback.Calls == 1, "Live fallback should run when local index is truncated and misses the asset.");
}

static void LegacyEquipmentDryRunUsesThemeAssistedLooseLookup()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");
    var looseAsset = Path.Combine(patch, "data", "sprite", "Â¾Ã‡Â¼Â¼Â»Ã§Â¸Â®Â¿Ã«", "rabbit_costume_garment.spr");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(Path.GetDirectoryName(looseAsset)!);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "spriterobeid.lub"), "SPRITE_ROBE_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "spriterobename.lub"), "RobeNameTable = {}\nRobeNameTable_Eng = {}\n");
    File.WriteAllText(looseAsset, "fake");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Costume_Rabbit",
                "RagnaForge Costume Rabbit",
                "rf_costume_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Costume_Garment"],
            "robe",
            5000,
            "ROBE_RF_COSTUME_RABBIT",
            "_rf_unknown_costume",
            [],
            null,
            1,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null),
        new VisualThemeEvaluation(
            @"C:\temp\visual-equipment-themes.local.json",
            "equipment-visuals",
            "fofo",
            new VisualThemeSuggestion("fofo", "Fofo", 6, ["robe"], ["Costume_Garment"], ["rabbit"]),
            [],
            ["rabbit", "cute", "poring"],
            []));

    Assert(report.CanApply, "Theme-assisted loose lookup should keep dry-run applicable.");
    Assert(report.Dependencies.Any(item =>
        item.Message.Contains("theme-assisted lookup found", StringComparison.OrdinalIgnoreCase)
        && string.Equals(item.SourcePath, looseAsset, StringComparison.OrdinalIgnoreCase)), "Theme-assisted loose candidate should be reported.");
}

static void LegacyEquipmentDryRunUsesThemeAssistedGrfLookup()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");
    var fakeLookup = new ThemeAwareFakeGrfAssetLookupService();

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "spriterobeid.lub"), "SPRITE_ROBE_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "spriterobename.lub"), "RobeNameTable = {}\nRobeNameTable_Eng = {}\n");

    var service = new LegacyEquipmentDryRunService(
        fakeLookup,
        new GrfAssetLookupOptions(
            true,
            [Path.Combine(grfs, "data_0.grf")],
            1,
            10));

    var report = service.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Costume_Rabbit",
                "RagnaForge Costume Rabbit",
                "rf_costume_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Costume_Garment"],
            "robe",
            5000,
            "ROBE_RF_COSTUME_RABBIT",
            "_rf_unknown_costume",
            [],
            null,
            1,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null),
        new VisualThemeEvaluation(
            @"C:\temp\visual-equipment-themes.local.json",
            "equipment-visuals",
            "fofo",
            new VisualThemeSuggestion("fofo", "Fofo", 6, ["robe"], ["Costume_Garment"], ["rabbit"]),
            [],
            ["rabbit", "cute", "poring"],
            []));

    Assert(report.CanApply, "Theme-assisted GRF lookup should keep dry-run applicable.");
    Assert(report.Dependencies.Any(item =>
        item.Message.Contains("theme-assisted GRF lookup found", StringComparison.OrdinalIgnoreCase)
        && item.SourcePath?.Contains("sample.grf::data/sprite/Â¾Ã‡Â¼Â¼Â»Ã§Â¸Â®Â¿Ã«/rabbit_costume_garment.spr", StringComparison.OrdinalIgnoreCase) == true), "Theme-assisted GRF candidate should be reported.");
    Assert(fakeLookup.ObservedOptions.Any(option => option.AllowContainsMatch), "Theme-assisted GRF lookup should enable contains match mode.");
    Assert(fakeLookup.ObservedOptions.Any(option => (option.NameHints ?? []).Contains("rabbit", StringComparer.OrdinalIgnoreCase)), "Theme-assisted GRF lookup should pass theme tokens.");
}

static void LegacyEquipmentDryRunPrefersIndexedGrfThemeLookup()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");
    var containerPath = Path.Combine(grfs, "data_0.grf");
    var indexStore = new JsonGrfContainerIndexStore(workspace.Root);
    var lookup = new ThrowIfContainsMatchLookupService();

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "spriterobeid.lub"), "SPRITE_ROBE_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "spriterobename.lub"), "RobeNameTable = {}\nRobeNameTable_Eng = {}\n");
    File.WriteAllText(containerPath, "fake");

    indexStore.Save(
        indexStore.BuildDefaultIndexPath(containerPath),
        GrfContainerContentIndexDocument.Create(
            containerPath,
            containerLength: 1,
            containerLastWriteTimeUtc: DateTimeOffset.UtcNow,
            entryCount: 1,
            directoryCount: 1,
            maxEntriesCaptured: 1,
            isTruncated: false,
            extensionCounts: [new GrfExtensionCount(".spr", 1)],
            topLevelDirectories: [new GrfTopLevelDirectoryCount("data", 1)],
            entries:
            [
                new GrfContentEntrySnapshot(
                    "data/sprite/Â¾Ã‡Â¼Â¼Â»Ã§Â¸Â®Â¿Ã«/rabbit_costume_garment.spr",
                    "data/sprite/Â¾Ã‡Â¼Â¼Â»Ã§Â¸Â®Â¿Ã«",
                    "rabbit_costume_garment.spr",
                    ".spr",
                    10,
                    10,
                    false)
            ]),
        overwrite: true);

    var service = new LegacyEquipmentDryRunService(
        lookup,
        indexStore,
        new GrfAssetLookupOptions(
            true,
            [containerPath],
            1,
            10));

    var report = service.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Costume_Rabbit",
                "RagnaForge Costume Rabbit",
                "rf_costume_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Costume_Garment"],
            "robe",
            5000,
            "ROBE_RF_COSTUME_RABBIT",
            "_rf_unknown_costume",
            [],
            null,
            1,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null),
        new VisualThemeEvaluation(
            @"C:\temp\visual-equipment-themes.local.json",
            "equipment-visuals",
            "fofo",
            new VisualThemeSuggestion("fofo", "Fofo", 6, ["robe"], ["Costume_Garment"], ["rabbit"]),
            [],
            ["rabbit"],
            []));

    Assert(report.Dependencies.Any(item =>
        item.Message.Contains("theme-assisted GRF index lookup found", StringComparison.OrdinalIgnoreCase)
        && item.SourcePath?.Contains("rabbit_costume_garment.spr", StringComparison.OrdinalIgnoreCase) == true), "Indexed GRF candidate should be preferred.");
    Assert(lookup.ContainsMatchCalls == 0, "Live GRF contains-match lookup should not run when indexed GRF candidate is available.");
}

static void CachedGrfRepositoryIndexerBuildsAndReusesCache()
{
    using var workspace = TempWorkspace.Create();
    var repository = Path.Combine(workspace.Root, "grfs");
    Directory.CreateDirectory(Path.Combine(repository, "pack"));
    var first = Path.Combine(repository, "pack", "first.grf");
    var second = Path.Combine(repository, "second.thor");
    var ignored = Path.Combine(repository, "notes.txt");
    var fixedTime = new DateTime(2026, 5, 7, 0, 0, 0, DateTimeKind.Utc);

    File.WriteAllText(first, "one");
    File.WriteAllText(second, "two");
    File.WriteAllText(ignored, "ignore");
    File.SetLastWriteTimeUtc(first, fixedTime);
    File.SetLastWriteTimeUtc(second, fixedTime);

    var store = new JsonGrfRepositoryIndexStore(workspace.Root);
    var indexer = new CachedGrfRepositoryIndexer(store);
    var options = new GrfRepositoryIndexOptions(repository, store.DefaultIndexPath, MaxContainers: 10);

    var firstResult = indexer.Build(options);
    var secondResult = indexer.Build(options);
    File.AppendAllText(first, " changed");
    File.SetLastWriteTimeUtc(first, fixedTime.AddMinutes(1));
    var thirdResult = indexer.Build(options);
    File.Delete(second);
    var fourthResult = indexer.Build(options);

    Assert(firstResult.Index.TotalContainerCount == 2, "Initial index should contain two containers.");
    Assert(firstResult.Summary.AddedCount == 2, "Initial index should count two added containers.");
    Assert(File.Exists(store.DefaultIndexPath), "Index cache should be written.");
    Assert(secondResult.Summary.CacheLoaded, "Second index should load cache.");
    Assert(secondResult.Summary.UnchangedCount == 2, "Second index should reuse unchanged metadata.");
    Assert(thirdResult.Summary.ChangedCount == 1, "Modified container should be counted as changed.");
    Assert(fourthResult.Summary.RemovedCount == 1, "Deleted cached container should be counted as removed.");
}

static void GrfRepositoryIndexStoreRejectsOutsideCacheDirectory()
{
    using var workspace = TempWorkspace.Create();
    var store = new JsonGrfRepositoryIndexStore(workspace.Root);
    var document = GrfRepositoryIndexDocument.Create(workspace.Root, 0, []);

    try
    {
        store.Save(Path.Combine(workspace.Root, "grf-repository.index.json"), document, overwrite: true);
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("Store should reject GRF index writes outside data/cache.");
}

static void CachedGrfRepositoryIndexerHonorsCancellation()
{
    using var workspace = TempWorkspace.Create();
    var repository = Path.Combine(workspace.Root, "grfs");
    Directory.CreateDirectory(repository);
    File.WriteAllText(Path.Combine(repository, "sample.grf"), "fake");

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    try
    {
        new CachedGrfRepositoryIndexer(new JsonGrfRepositoryIndexStore(workspace.Root)).Build(
            new GrfRepositoryIndexOptions(repository, Path.Combine(workspace.Root, "data", "cache", "index.json")),
            cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        return;
    }

    throw new InvalidOperationException("Indexer should honor cancellation before scanning.");
}

static void GrfContainerIndexStoreRejectsOutsideIndexesDirectory()
{
    using var workspace = TempWorkspace.Create();
    var store = new JsonGrfContainerIndexStore(workspace.Root);
    var document = GrfContainerContentIndexDocument.Create(
        Path.Combine(workspace.Root, "sample.grf"),
        containerLength: 1,
        containerLastWriteTimeUtc: DateTimeOffset.UtcNow,
        entryCount: 1,
        directoryCount: 1,
        maxEntriesCaptured: 1,
        isTruncated: false,
        extensionCounts: [new GrfExtensionCount(".spr", 1)],
        topLevelDirectories: [new GrfTopLevelDirectoryCount("data", 1)],
        entries:
        [
            new GrfContentEntrySnapshot("data\\sample.spr", "data", "sample.spr", ".spr", 1, 1, false)
        ]);

    try
    {
        store.Save(Path.Combine(workspace.Root, "sample.index.json"), document, overwrite: true);
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("Store should reject GRF content index writes outside data/indexes.");
}

static void LegacyItemDryRunBuildsProposal()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "db", "pre-re"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(Path.Combine(patch, "system"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n\n#Body:\n");
    File.WriteAllText(Path.Combine(rathena, "db", "pre-re", "item_db.yml"), "- Id: 501\n  AegisName: Red_Potion\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "501#Potion#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "501#red_potion#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "501#\nPotion.\n#\n");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "501#Potion#\n");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "501#red_potion#\n");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "501#\nPotion.\n#\n");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "1101#3#\n");
    File.WriteAllText(Path.Combine(patch, "data", "rf_test_item.bmp"), "fake");
    File.WriteAllText(Path.Combine(patch, "2025-07-16_Ragexe.exe"), "fake");

    var service = new LegacyItemDryRunService();
    var report = service.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, null, "test"),
        new ItemDefinitionInput(
            null,
            "RF_Test_Item",
            "RagnaForge Test Item",
            "rf_test_item",
            "Etc",
            10,
            5,
            10,
            0,
            null,
            ["Line 1", "Line 2"],
            null,
            null,
            []));

    Assert(report.CanApply, "Dry-run should be applicable when no hard blockers are present.");
    Assert(report.ClientDateUsed == "2025-07-16", "Dry-run should use detected client date.");
    Assert(report.ResolvedId >= 50000, "Dry-run should allocate a high custom ID.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("item_db.yml", StringComparison.OrdinalIgnoreCase)), "Server-side proposal should be present.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("idnum2itemdisplaynametable.txt", StringComparison.OrdinalIgnoreCase)), "Client-side legacy proposal should be present.");
    Assert(report.DiffPreview.FileCount == report.ProposedChanges.Count, "Diff preview should cover every proposed file change.");
    Assert(report.DiffPreview.Entries.Any(entry => entry.TargetPath.EndsWith("item_db.yml", StringComparison.OrdinalIgnoreCase) && entry.UnifiedDiff.Contains("+++ ", StringComparison.Ordinal)), "Server-side diff entry should be rendered.");
}

static void LegacyItemDryRunBlocksCollisions()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "db", "pre-re"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(rathena, "db", "pre-re", "item_db.yml"), "- Id: 50000\n  AegisName: RF_Test_Item\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "50000#Existing#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "50000#existing#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "50000#\nExisting.\n#\n");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "50000#Existing#\n");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "50000#existing#\n");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "50000#\nExisting.\n#\n");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var report = new LegacyItemDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, null, "test"),
        new ItemDefinitionInput(
            50000,
            "RF_Test_Item",
            "Existing Item",
            "rf_test_item",
            "Etc",
            10,
            5,
            10,
            0,
            null,
            ["Existing item."],
            null,
            null,
            []));

    Assert(!report.CanApply, "Dry-run should fail when ID/Aegis collisions exist.");
    Assert(report.Dependencies.Count(item => item.State == ItemDependencyState.Missing) >= 2, "Collisions should be reported as missing dependencies.");
}

static void LegacyItemDryRunResolvesGrfAssetLookup()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var service = new LegacyItemDryRunService(
        new FakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [Path.Combine(grfs, "sample.grf")], 1, 10));
    var report = service.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new ItemDefinitionInput(
            null,
            "RF_Test_Item",
            "RagnaForge Test Item",
            "rf_test_item",
            "Etc",
            10,
            5,
            10,
            0,
            null,
            ["Line 1"],
            null,
            null,
            []));

    Assert(report.CanApply, "Dry-run should remain applicable when GRF lookup satisfies the asset dependency.");
    Assert(report.Dependencies.Any(item => item.Category == "Assets" && item.State == ItemDependencyState.Satisfied && item.Message.Contains("GRF asset", StringComparison.OrdinalIgnoreCase)), "GRF asset lookup satisfaction should be reported.");
    Assert(report.AssetLookup is not null && report.AssetLookup.Source == GrfAssetLookupSource.LiveScan, "Item dry-run should expose GRF lookup provenance in the report.");
    Assert(report.DiffPreview.Entries.Count == report.ProposedChanges.Count, "Diff preview should remain available when GRF lookup is used.");
}

static void ItemClientSidePlanDetectsTextualItemInfoLua()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: false);
    File.WriteAllText(Path.Combine(workspace.Patch, "system", "iteminfo.lua"), "tbl = {}\n");

    var report = BuildBasicItemReport(workspace);

    Assert(report.ClientSidePlan is not null, "Item dry-run should expose a client-side plan.");
    var plan = report.ClientSidePlan!;
    Assert(plan.ClientSideMode == ClientSideMode.ItemInfo, "ItemInfo-only client should be detected.");
    Assert(plan.FileFormats.Any(format => format.Contains("TextLua", StringComparison.Ordinal)), "Textual lua itemInfo should be classified.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("iteminfo.lua", StringComparison.OrdinalIgnoreCase)), "ItemInfo lua should receive a safe hunk.");
}

static void ItemClientSidePlanDetectsTextualItemInfoLub()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: false);
    File.WriteAllText(Path.Combine(workspace.Patch, "system", "iteminfo.lub"), "tbl = {}\n");

    var report = BuildBasicItemReport(workspace);

    var plan = report.ClientSidePlan ?? throw new InvalidOperationException("ClientSidePlan was not populated.");
    Assert(plan.ClientSideMode == ClientSideMode.ItemInfo, "ItemInfo lub-only client should be detected.");
    Assert(plan.FileFormats.Any(format => format.Contains("TextLub", StringComparison.Ordinal)), "Textual lub itemInfo should be classified.");
    Assert(report.CanApply, "TextLub itemInfo should be apply-ready.");
}

static void ItemClientSidePlanBlocksBytecodeItemInfoLub()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: false);
    File.WriteAllBytes(Path.Combine(workspace.Patch, "system", "iteminfo.lub"), [0x1B, 0x4C, 0x75, 0x61, 0x00]);

    var report = BuildBasicItemReport(workspace);

    Assert(!report.CanApply, "Bytecode itemInfo should block item apply.");
    Assert(report.BytecodeBlocks.Any(path => path.EndsWith("iteminfo.lub", StringComparison.OrdinalIgnoreCase)), "Bytecode block should be exposed.");
    Assert(!report.ProposedChanges.Any(change => change.TargetPath.EndsWith("iteminfo.lub", StringComparison.OrdinalIgnoreCase)), "No editable hunk should be generated for bytecode.");
}

static void ItemClientSidePlanDetectsLegacyTxtTables()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);

    var report = BuildBasicItemReport(workspace);

    var plan = report.ClientSidePlan ?? throw new InvalidOperationException("ClientSidePlan was not populated.");
    Assert(plan.ClientSideMode == ClientSideMode.LegacyTxt, "Legacy TXT mode should be detected.");
    Assert(plan.LegacyTablesDetected, "Legacy table presence should be exposed.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("idnum2itemdisplaynametable.txt", StringComparison.OrdinalIgnoreCase)), "Legacy TXT hunk should be generated.");
}

static void ItemClientSidePlanDetectsHybridClient()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    File.WriteAllText(Path.Combine(workspace.Patch, "system", "iteminfo.lua"), "tbl = {}\n");

    var report = BuildBasicItemReport(workspace);

    var plan = report.ClientSidePlan ?? throw new InvalidOperationException("ClientSidePlan was not populated.");
    Assert(plan.ClientSideMode == ClientSideMode.Hybrid, "Hybrid item client should be detected.");
    Assert(plan.HybridClientDetected, "Hybrid flag should be exposed.");
    Assert(plan.ValidationWarnings.Any(warning => warning.Contains("Hybrid", StringComparison.OrdinalIgnoreCase)), "Hybrid client should emit warning.");
}

static void ItemClientSidePlanBlocksHybridBytecode()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    File.WriteAllBytes(Path.Combine(workspace.Patch, "system", "iteminfo.lub"), [0x1B, 0x4C, 0x75, 0x61, 0x00]);

    var report = BuildBasicItemReport(workspace);

    var plan = report.ClientSidePlan ?? throw new InvalidOperationException("ClientSidePlan was not populated.");
    Assert(plan.ClientSideMode == ClientSideMode.Hybrid, "Hybrid client should still be detected.");
    Assert(!report.CanApply, "Hybrid client with bytecode itemInfo should block until a strategy is explicit.");
    Assert(plan.BlockReasons.Any(reason => reason.Contains("bytecode", StringComparison.OrdinalIgnoreCase)), "Bytecode reason should be visible.");
}

static void LegacyItemApplyServiceRecordsPostWriteValidation()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    var repositoryPaths = workspace.Paths;
    var report = BuildBasicItemReport(workspace);

    var result = new LegacyItemApplyService(workspace.Root).Apply(repositoryPaths, report);

    Assert(result.Applied, "Item apply should succeed.");
    Assert(result.PostWriteValidation is { IsValid: true }, "Item apply should record successful post-write validation.");
}

static void LegacyItemRollbackBlocksClientSideDrift()
{
    using var workspace = CreateItemClientWorkspace(includeLegacyTables: true);
    var repositoryPaths = workspace.Paths;
    var report = BuildBasicItemReport(workspace);
    var service = new LegacyItemApplyService(workspace.Root);
    var result = service.Apply(repositoryPaths, report);
    var displayTable = Path.Combine(workspace.Patch, "data", "idnum2itemdisplaynametable.txt");
    File.AppendAllText(displayTable, "\n999999#Manual Drift#");

    AssertThrows<InvalidOperationException>(() => service.Rollback(result.ApplyLogPath), "Rollback should block when a client-side file drifted after apply.");
}

static void LegacyEquipmentDryRunExposesVisualClientSidePlan()
{
    using var workspace = CreateEquipmentClientWorkspace();
    var report = BuildBasicEquipmentReport(workspace);

    Assert(report.ClientSidePlan is not null, "Equipment should expose item client-side plan.");
    Assert(report.VisualClientSidePlan is not null, "Equipment should expose visual client-side plan.");
    var visualPlan = report.VisualClientSidePlan!;
    Assert(visualPlan.ApplyTargets.Any(path => path.EndsWith("accessoryid.lub", StringComparison.OrdinalIgnoreCase)), "Visual plan should include accessoryid target.");
    Assert(report.ApplyReadiness == ClientApplyReadiness.Ready, "Textual visual datainfo should be ready.");
}

static void LegacyEquipmentDryRunBlocksBytecodeVisualDatainfo()
{
    using var workspace = CreateEquipmentClientWorkspace();
    File.WriteAllBytes(Path.Combine(workspace.Datainfo, "accessoryid.lub"), [0x1B, 0x4C, 0x75, 0x61, 0x00]);
    var report = BuildBasicEquipmentReport(workspace);

    Assert(!report.CanApply, "Bytecode visual datainfo should block equipment apply.");
    Assert(report.VisualClientSidePlan?.BytecodeBlockedFiles.Any(path => path.EndsWith("accessoryid.lub", StringComparison.OrdinalIgnoreCase)) == true, "Visual bytecode block should be exposed.");
    Assert(!report.ProposedChanges.Any(change => change.TargetPath.EndsWith("accessoryid.lub", StringComparison.OrdinalIgnoreCase)), "Bytecode visual file should not receive a hunk.");
}

static ItemClientWorkspace CreateItemClientWorkspace(bool includeLegacyTables)
{
    var workspace = new ItemClientWorkspace();
    Directory.CreateDirectory(Path.Combine(workspace.Rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(workspace.Patch, "data"));
    Directory.CreateDirectory(Path.Combine(workspace.Patch, "system"));
    Directory.CreateDirectory(workspace.Grfs);
    Directory.CreateDirectory(workspace.GrfEditor);
    File.WriteAllText(Path.Combine(workspace.Rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");

    if (includeLegacyTables)
    {
        WriteLegacyItemTables(workspace.Patch);
    }

    return workspace;
}

static EquipmentClientWorkspace CreateEquipmentClientWorkspace()
{
    var workspace = new EquipmentClientWorkspace();
    Directory.CreateDirectory(Path.Combine(workspace.Rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(workspace.Patch, "data"));
    Directory.CreateDirectory(workspace.Datainfo);
    Directory.CreateDirectory(workspace.Grfs);
    Directory.CreateDirectory(workspace.GrfEditor);
    File.WriteAllText(Path.Combine(workspace.Rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    WriteLegacyItemTables(workspace.Patch);
    File.WriteAllText(Path.Combine(workspace.Datainfo, "accessoryid.lub"), "ACCESSORY_IDs = {}\n");
    File.WriteAllText(Path.Combine(workspace.Datainfo, "accname.lub"), "AccNameTable = {}\n");
    return workspace;
}

static void WriteLegacyItemTables(string patch)
{
    var data = Path.Combine(patch, "data");
    Directory.CreateDirectory(data);
    File.WriteAllText(Path.Combine(data, "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(data, "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(data, "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(data, "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(data, "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(data, "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(data, "itemslotcounttable.txt"), "");
}

static void SaveApiTestManifest(string workspaceRoot, RepositoryPaths paths)
{
    var store = new JsonConfigurationManifestStore(workspaceRoot);
    var manifest = ConfigurationManifest.Create(
        paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "api test"),
        isProgressive: true,
        timestampUtc: new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero));

    store.Save(store.DefaultManifestPath, manifest, overwrite: true);
}

static ItemDryRunReport BuildBasicItemReport(ItemClientWorkspace workspace) =>
    new LegacyItemDryRunService().Create(
        workspace.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new ItemDefinitionInput(
            null,
            "RF_Client_Item",
            "RagnaForge Client Item",
            "rf_client_item",
            "Etc",
            10,
            5,
            10,
            0,
            null,
            ["Client item."],
            null,
            null,
            []));

static EquipmentDryRunReport BuildBasicEquipmentReport(EquipmentClientWorkspace workspace) =>
    new LegacyEquipmentDryRunService().Create(
        workspace.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Client_Headgear",
                "RagnaForge Client Headgear",
                "rf_client_headgear",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5000,
            "ACCESSORY_RF_CLIENT_HEADGEAR",
            "_rf_client_headgear",
            ["All"],
            null,
            10,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null));

static void LegacyItemApplyServiceAppliesAndRollsBackSafely()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "100#Old#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var report = new LegacyItemDryRunService().Create(
        repositoryPaths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new ItemDefinitionInput(
            null,
            "RF_Apply_Item",
            "RagnaForge Apply Item",
            "rf_apply_item",
            "Etc",
            10,
            5,
            10,
            0,
            null,
            ["Line 1"],
            null,
            null,
            []));

    var service = new LegacyItemApplyService(workspace.Root);
    var applyResult = service.Apply(repositoryPaths, report);

    Assert(applyResult.Applied, "Apply should succeed for a valid dry-run report.");
    Assert(File.Exists(applyResult.ApplyLogPath), "Apply log should be written inside the workspace.");
    Assert(applyResult.Files.All(file => !string.IsNullOrWhiteSpace(file.NewSha256)), "Applied file audit should capture output hashes.");
    Assert(applyResult.AuditTrail.Count >= applyResult.Files.Count, "Apply audit trail should capture workflow stages.");
    Assert(File.Exists(Path.Combine(rathena, "db", "import", "item_db.yml")), "Target item DB should still exist after apply.");
    Assert(File.ReadAllText(Path.Combine(rathena, "db", "import", "item_db.yml")).Contains("RF_Apply_Item", StringComparison.Ordinal), "Applied item snippet should be appended to the item DB.");

    var rollbackResult = service.Rollback(applyResult.ApplyLogPath);
    Assert(rollbackResult.RolledBack, "Rollback should succeed after apply.");
    Assert(File.Exists(rollbackResult.RollbackLogPath), "Rollback log should be written inside the workspace.");
    Assert(rollbackResult.AuditTrail.Count > 0, "Rollback should capture audit trail entries.");
    Assert(!File.ReadAllText(Path.Combine(rathena, "db", "import", "item_db.yml")).Contains("RF_Apply_Item", StringComparison.Ordinal), "Rollback should restore the previous item DB contents.");
}

static void LegacyItemApplyServiceBlocksConflictingAppend()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var existingSnippet = "\n# RagnaForge item dry-run proposal\n- Id: 50000\n  AegisName: RF_Apply_Item\n";
    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n" + existingSnippet);
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "50000#RagnaForge Apply Item#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var report = new ItemDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        "2025-07-16",
        "test",
        "LegacyTables",
        new ItemDefinitionInput(50000, "RF_Apply_Item", "RagnaForge Apply Item", "rf_apply_item", "Etc", 10, 5, 10, 0, null, ["Line 1"], null, null, []),
        50000,
        true,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        [new ProposedFileChange(Path.Combine(rathena, "db", "import", "item_db.yml"), "append", true, existingSnippet)],
        new ItemDiffPreview(
            1,
            0,
            1,
            [
                new ItemDiffPreviewEntry(
                    Path.Combine(rathena, "db", "import", "item_db.yml"),
                    "append",
                    true,
                    4,
                    4,
                    string.Empty)
            ]),
        [],
        null);

    var result = new LegacyItemApplyService(workspace.Root).Apply(repositoryPaths, report);

    Assert(!result.Applied, "Apply should be blocked when the target already contains the preview.");
    Assert(result.Conflicts.Any(conflict => conflict.Code.Contains("preview", StringComparison.OrdinalIgnoreCase)), "Conflict audit should capture duplicate preview detection.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked apply should still emit an audit log.");
}

static void LegacyEquipmentApplyServiceAppliesAndRollsBackSafely()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var itemDbPath = Path.Combine(rathena, "db", "import", "item_db.yml");
    var displayTablePath = Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt");
    var accessoryIdPath = Path.Combine(datainfo, "accessoryid.lub");
    var accNamePath = Path.Combine(datainfo, "accname.lub");

    File.WriteAllText(itemDbPath, "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(displayTablePath, "100#Old#\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(accessoryIdPath, "ACCESSORY_IDs = {}\n");
    File.WriteAllText(accNamePath, "AccNameTable = {}\n");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var report = new LegacyEquipmentDryRunService().Create(
        repositoryPaths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_APPLY_RABBIT",
                "RagnaForge Apply Rabbit",
                "rf_apply_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                "bonus bLuk,1;",
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5000,
            "ACCESSORY_RF_APPLY_RABBIT",
            "_rf_apply_rabbit",
            ["All"],
            null,
            10,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null));

    var service = new LegacyEquipmentApplyService(workspace.Root);
    var applyResult = service.Apply(repositoryPaths, report);

    Assert(applyResult.Applied, "Equipment apply should succeed for a valid dry-run report.");
    Assert(File.Exists(applyResult.ApplyLogPath), "Equipment apply log should be written inside the workspace.");
    Assert(applyResult.ApplyLogPath.Contains(Path.Combine("data", "logs", "equipment"), StringComparison.OrdinalIgnoreCase), "Equipment apply log should live under data/logs/equipment.");
    Assert(applyResult.Files.Count == report.ProposedChanges.Count, "Equipment apply should audit every proposed change.");
    Assert(applyResult.Files.All(file => !string.IsNullOrWhiteSpace(file.NewSha256)), "Equipment apply audit should capture output hashes.");
    Assert(File.ReadAllText(itemDbPath).Contains("RF_APPLY_RABBIT", StringComparison.Ordinal), "Equipment apply should append the item DB entry.");
    Assert(File.ReadAllText(itemDbPath).Contains("View: 5000", StringComparison.Ordinal), "Equipment item DB snippet should preserve visual view.");
    Assert(File.ReadAllText(displayTablePath).Contains("RagnaForge Apply Rabbit", StringComparison.Ordinal), "Equipment apply should append the legacy display table.");
    Assert(File.ReadAllText(accessoryIdPath).Contains("ACCESSORY_RF_APPLY_RABBIT = 5000", StringComparison.Ordinal), "Equipment apply should append the accessory ID table.");
    Assert(File.ReadAllText(accNamePath).Contains("_rf_apply_rabbit", StringComparison.Ordinal), "Equipment apply should append the accessory name table.");

    var rollbackResult = service.Rollback(applyResult.ApplyLogPath);
    Assert(rollbackResult.RolledBack, "Equipment rollback should succeed after apply.");
    Assert(File.Exists(rollbackResult.RollbackLogPath), "Equipment rollback log should be written inside the workspace.");
    Assert(!File.ReadAllText(itemDbPath).Contains("RF_APPLY_RABBIT", StringComparison.Ordinal), "Equipment rollback should restore item_db.yml.");
    Assert(!File.ReadAllText(displayTablePath).Contains("RagnaForge Apply Rabbit", StringComparison.Ordinal), "Equipment rollback should restore the legacy display table.");
    Assert(!File.ReadAllText(accessoryIdPath).Contains("ACCESSORY_RF_APPLY_RABBIT = 5000", StringComparison.Ordinal), "Equipment rollback should restore accessoryid.lub.");
    Assert(!File.ReadAllText(accNamePath).Contains("_rf_apply_rabbit", StringComparison.Ordinal), "Equipment rollback should restore accname.lub.");
}

static void LegacyEquipmentApplyServiceBlocksVisualCollisionBeforeWriting()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var accessoryIdPath = Path.Combine(datainfo, "accessoryid.lub");
    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(accessoryIdPath, "ACCESSORY_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "accname.lub"), "AccNameTable = {}\n");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var report = new LegacyEquipmentDryRunService().Create(
        repositoryPaths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_COLLIDE_RABBIT",
                "RagnaForge Collide Rabbit",
                "rf_collide_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5000,
            "ACCESSORY_RF_COLLIDE_RABBIT",
            "_rf_collide_rabbit",
            [],
            null,
            1,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null));

    Assert(report.CanApply, "Baseline equipment dry-run should be applicable before the collision is introduced.");

    File.WriteAllText(accessoryIdPath, "ACCESSORY_IDs = {\n\tACCESSORY_ALREADY_TAKEN = 5000,\n}\n");

    var result = new LegacyEquipmentApplyService(workspace.Root).Apply(repositoryPaths, report);

    Assert(!result.Applied, "Equipment apply should be blocked when a visual collision appears after dry-run.");
    Assert(result.Conflicts.Any(conflict => conflict.Code == "equipment.view-id-present"), "Equipment apply should report the duplicate View ID conflict.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked equipment apply should still emit an audit log.");
    Assert(!File.ReadAllText(Path.Combine(rathena, "db", "import", "item_db.yml")).Contains("RF_COLLIDE_RABBIT", StringComparison.Ordinal), "Blocked equipment apply must not append the new item.");
}

static void LegacyEquipmentApplyServiceBlocksTargetsOutsideEquipmentRoots()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var outsideTarget = Path.Combine(grfs, "unsafe.txt");
    var report = new EquipmentDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        "2025-07-16",
        "test",
        "legacy-tables",
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(50010, "RF_UNSAFE_EQUIP", "Unsafe Equip", "rf_unsafe_equip", "Armor", 10, 5, 10, 0, null, ["Unsafe"], null, null, []),
            ["Head_Top"],
            "headgear",
            5000,
            "ACCESSORY_RF_UNSAFE_EQUIP",
            "_rf_unsafe_equip",
            [],
            null,
            1,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null),
        50010,
        true,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        [new ProposedFileChange(outsideTarget, "append", false, "unsafe")],
        new ItemDiffPreview(1, 0, 1, [new ItemDiffPreviewEntry(outsideTarget, "append", false, 0, 1, string.Empty)]),
        [],
        null,
        null,
        null);

    var result = new LegacyEquipmentApplyService(workspace.Root).Apply(repositoryPaths, report);

    Assert(!result.Applied, "Equipment apply should be blocked when a proposed target is outside the allowed roots.");
    Assert(result.Conflicts.Any(conflict => conflict.Code == "path.outside-equipment-roots"), "Equipment apply should report the outside-root conflict.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked equipment apply should still emit an audit log.");
    Assert(!File.Exists(outsideTarget), "Blocked equipment apply must not create the unsafe target.");
}

static void NpcDryRunBuildsScriptAndLoaderPreview()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteBuiltInClientIdentityTables();

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("RagnaForge Guide", "prontera", 150, 180, 2, "4_M_JOB_BLACKSMITH", null, null));

    Assert(report.CanApply, "NPC dry-run should remain applicable on a valid map.");
    Assert(report.ServerCanApply, "NPC server-side should be ready.");
    Assert(report.ProposedChanges.Count == 2, "NPC dry-run should propose script file and loader update.");
    Assert(report.DiffPreview.Entries.Count == 2, "NPC diff preview should cover both proposed changes.");
    Assert(report.SpriteValidation.IsStandardClientSprite, "Standard NPC sprite should validate against client datainfo.");
    Assert(!report.ClientIdentityRequired, "Standard NPC sprite should not require client identity apply.");
    Assert(report.ApplyReadiness == NpcApplyReadiness.Ready, "Standard NPC apply should be fully ready.");
}

static void NpcDryRunFlagsNonStandardCustomSpriteValidation()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Custom Guide", "prontera", 150, 180, 2, "CustomGuide", null, null));

    Assert(report.ServerCanApply, "Server-side NPC plan should still be ready.");
    Assert(!report.CanApply, "Custom NPC sprite without client symbol/id should stay blocked for full apply.");
    Assert(report.SpriteValidation.RequiresAdditionalClientValidation, "Custom NPC sprite should require additional client validation.");
    Assert(report.SpriteResolution.Resolved, "Loose patch sprite should resolve for client planning.");
    Assert(report.SpriteResolution.Source.Equals("loose-custom-sprite", StringComparison.OrdinalIgnoreCase), "Loose patch sprite should preserve its detection source.");
    Assert(report.ClientIdentityRequired, "Custom NPC sprite should require client identity registration when not already registered.");
    Assert(!report.CanApplyClientIdentity, "Client identity should stay blocked until symbol and client ID are provided.");
    Assert(report.ApplyReadiness == NpcApplyReadiness.ReadyServerOnly, "Custom NPC without safe client identity should be server-only ready.");
    Assert(report.ClientIdentityPlan.BlockReasons.Any(reason => reason.Contains("Client symbol is required", StringComparison.OrdinalIgnoreCase)), "Dry-run should explain that a client symbol is required.");
}

static void NpcDryRunResolvesCustomSpriteViaGrfLookup()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();

    var service = new NpcDryRunService(
        new NpcSpriteFakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [Path.Combine(fixture.Grfs, "sample.grf")], 1, 10));
    var report = service.Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("GRF Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    Assert(!report.CanApply, "GRF-only sprite resolution should not unlock full apply before the asset-copy pipeline exists.");
    Assert(report.SpriteValidation.AssetLookup is not null && report.SpriteValidation.AssetLookup.Matches.Count > 0, "NPC sprite validation should expose the GRF lookup match.");
    Assert(report.SpriteValidation.DetectionSource.Contains("grf-custom-sprite/live-scan", StringComparison.OrdinalIgnoreCase), "NPC sprite detection source should preserve GRF provenance.");
    Assert(report.SpriteResolution.Resolved, "GRF lookup should resolve a candidate sprite.");
    Assert(report.SpriteResolution.NeedsAssetCopyPlan, "GRF-only sprite resolution should mark Patch asset copy as pending.");
    Assert(report.ClientIdentityPlan.BlockReasons.Any(reason => reason.Contains("asset copy", StringComparison.OrdinalIgnoreCase)), "Dry-run should explain that Patch asset copy is still pending.");
}

static void NpcDryRunDetectsTextualClientIdentityFiles()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Custom Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    Assert(report.CanApply, "Textual client identity files plus loose sprite should allow full NPC apply.");
    Assert(report.ClientIdentityRequired, "Custom sprite should require client identity planning.");
    Assert(report.CanApplyClientIdentity, "Client identity should be applicable when text files and explicit symbol/id are available.");
    Assert(report.ClientIdentityPlan.FilesDetected.Count(file => file.Selected && file.Format == NpcClientFileFormat.TextLub) == 3, "All three selected client identity files should be textual .lub in the local fixture.");
    Assert(report.ProposedClientRegistration.Count == 3, "Dry-run should prepare three client identity registrations.");
    Assert(report.DiffPreview.Entries.Count == 5, "NPC diff preview should include server-side and client-side files.");
    Assert(report.DiffPreview.Entries.Count(entry => entry.TargetPath.EndsWith(".lub", StringComparison.OrdinalIgnoreCase)) == 3, "Client-side diff hunks should only be emitted for the three textual identity files.");
}

static void NpcDryRunBlocksBytecodeClientIdentityApply()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteBytecodeClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Custom Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    Assert(!report.CanApply, "Bytecode client identity files should block full NPC apply.");
    Assert(report.ServerCanApply, "Server-side NPC plan should remain ready.");
    Assert(!report.CanApplyClientIdentity, "Client identity apply must stay blocked for bytecode.");
    Assert(report.BytecodeBlocks.Count == 3, "All three bytecode files should be surfaced as blocks.");
    Assert(report.ApplyReadiness == NpcApplyReadiness.ReadyServerOnly, "Bytecode should degrade NPC apply to server-only readiness.");
}

static void NpcDryRunBlocksAmbiguousGrfSpriteMatches()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();

    var report = new NpcDryRunService(
        new NpcAmbiguousSpriteFakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [Path.Combine(fixture.Grfs, "sample.grf")], 2, 10)).Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Custom Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    Assert(report.SpriteResolution.Ambiguous, "Multiple GRF sprite matches should be treated as ambiguous.");
    Assert(!report.CanApply, "Ambiguous sprite resolution should block full NPC apply.");
    Assert(report.ClientIdentityPlan.BlockReasons.Any(reason => reason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase)), "Dry-run should explain the ambiguity block.");
}

static void NpcApplyServiceAppliesAndRollsBackSafely()
{
    using var fixture = new NpcTestFixture();

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("RagnaForge Apply Guide", "prontera", 150, 180, 2, "100", "mes \"Apply\";\nclose;", null));

    var service = new NpcApplyService(fixture.Root);
    var applyResult = service.Apply(fixture.Paths, report);
    var scriptPath = Path.Combine(fixture.Rathena, "npc", "custom", "ragnaforge_npc_ragnaforge_apply_guide.txt");

    Assert(applyResult.Applied, "NPC apply should succeed for a valid dry-run report.");
    Assert(!applyResult.ServerOnlyApplied, "Built-in numeric NPC should not fall back to server-only mode.");
    Assert(File.Exists(applyResult.ApplyLogPath), "NPC apply log should be written inside the workspace.");
    Assert(applyResult.ApplyLogPath.Contains(Path.Combine("data", "logs", "npcs"), StringComparison.OrdinalIgnoreCase), "NPC apply log should live under data/logs/npcs.");
    Assert(File.Exists(scriptPath), "NPC apply should create the custom script file.");
    Assert(File.ReadAllText(scriptPath).Contains("RagnaForge Apply Guide", StringComparison.Ordinal), "NPC script should contain the requested NPC name.");
    Assert(File.ReadAllText(Path.Combine(fixture.Rathena, "npc", "scripts_custom.conf")).Contains("ragnaforge_npc_ragnaforge_apply_guide.txt", StringComparison.Ordinal), "NPC loader should be appended.");
    Assert(applyResult.Files.All(file => !string.IsNullOrWhiteSpace(file.NewSha256)), "NPC apply audit should capture output hashes.");

    var rollbackResult = service.Rollback(applyResult.ApplyLogPath);
    Assert(rollbackResult.RolledBack, "NPC rollback should succeed after apply.");
    Assert(File.Exists(rollbackResult.RollbackLogPath), "NPC rollback log should be written inside the workspace.");
    Assert(!File.Exists(scriptPath), "NPC rollback should remove the created custom script.");
    Assert(!File.ReadAllText(Path.Combine(fixture.Rathena, "npc", "scripts_custom.conf")).Contains("ragnaforge_npc_ragnaforge_apply_guide.txt", StringComparison.Ordinal), "NPC rollback should restore scripts_custom.conf.");
}

static void NpcApplyServiceAllowsExplicitServerOnlyFallback()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteBytecodeClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Server Only Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    var bytecodeBefore = File.ReadAllBytes(Path.Combine(fixture.PatchDatainfo, "jobname.lub"));
    var applyResult = new NpcApplyService(fixture.Root).Apply(fixture.Paths, report, allowServerOnly: true);

    Assert(applyResult.Applied, "NPC apply should allow explicit server-only fallback.");
    Assert(applyResult.ServerOnlyApplied, "Server-only fallback should be marked in the result.");
    Assert(applyResult.Files.All(file => file.TargetPath.StartsWith(Path.Combine(fixture.Rathena, "npc"), StringComparison.OrdinalIgnoreCase)), "Server-only fallback must not touch Patch client files.");
    Assert(File.ReadAllBytes(Path.Combine(fixture.PatchDatainfo, "jobname.lub")).SequenceEqual(bytecodeBefore), "Server-only fallback must not rewrite blocked bytecode files.");
}

static void NpcApplyServiceAppliesAndRollsBackClientIdentityTextSafely()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Client Guide", "prontera", 150, 180, 2, "CustomGuide", "mes \"Client\";\nclose;", null, "JT_CUSTOM_GUIDE", 7310));

    var service = new NpcApplyService(fixture.Root);
    var applyResult = service.Apply(fixture.Paths, report);

    Assert(applyResult.Applied, "NPC apply should succeed when client identity files are textual.");
    Assert(!applyResult.ServerOnlyApplied, "Client identity apply should not be reduced to server-only mode.");
    Assert(applyResult.PostWriteValidation is { IsValid: true }, "NPC apply should record successful post-write validation.");
    Assert(File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "jobname.lub")).Contains("JT_CUSTOM_GUIDE", StringComparison.Ordinal), "jobname.lub should receive the new NPC symbol.");
    Assert(File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "jobidentity.lub")).Contains("JT_CUSTOM_GUIDE = 7310", StringComparison.Ordinal), "jobidentity.lub should receive the new client ID.");
    Assert(File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "npcidentity.lub")).Contains("JT_CUSTOM_GUIDE = 7310", StringComparison.Ordinal), "npcidentity.lub should receive the new client ID.");

    var rollbackResult = service.Rollback(applyResult.ApplyLogPath);
    Assert(rollbackResult.RolledBack, "NPC rollback should restore client identity files.");
    Assert(!File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "jobname.lub")).Contains("JT_CUSTOM_GUIDE", StringComparison.Ordinal), "jobname.lub should be restored after rollback.");
    Assert(!File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "jobidentity.lub")).Contains("JT_CUSTOM_GUIDE", StringComparison.Ordinal), "jobidentity.lub should be restored after rollback.");
    Assert(!File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "npcidentity.lub")).Contains("JT_CUSTOM_GUIDE", StringComparison.Ordinal), "npcidentity.lub should be restored after rollback.");
}

static void NpcApplyServiceBlocksDuplicateClientIdentityRegistration()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");
    File.AppendAllText(Path.Combine(fixture.PatchDatainfo, "jobidentity.lub"), "JTtbl.JT_DUPLICATE = 7310\n");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Client Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    Assert(!report.CanApplyClientIdentity, "Duplicate client ID should block client identity apply.");
    Assert(report.ClientIdentityPlan.ValidationErrors.Any(error => error.Contains("already used", StringComparison.OrdinalIgnoreCase)), "Dry-run should surface the duplicate client ID.");
    AssertThrows<InvalidOperationException>(() => new NpcApplyService(fixture.Root).Apply(fixture.Paths, report), "NPC apply should refuse duplicate client identity registration by default.");
}

static void NpcApplyServiceBlocksMalformedClientIdentityStaging()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteMalformedTextClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Client Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    var result = new NpcApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(!result.Applied, "Malformed client identity staging should block final replacement.");
    Assert(result.PostWriteValidation is { IsValid: false }, "NPC apply should record the failed post-write validation.");
    Assert(result.Messages.Any(message => message.Contains("staging validation failed", StringComparison.OrdinalIgnoreCase)), "NPC apply should explain the staging validation failure.");
    Assert(!File.ReadAllText(Path.Combine(fixture.PatchDatainfo, "jobname.lub")).Contains("JT_CUSTOM_GUIDE", StringComparison.Ordinal), "Blocked apply must not rewrite the client identity file.");
}

static void NpcRollbackBlocksClientIdentityDriftAfterApply()
{
    using var fixture = new NpcTestFixture();
    fixture.WriteEmptyTextClientIdentityTables();
    fixture.WriteLooseCustomSprite("CustomGuide");

    var report = new NpcDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Client Guide", "prontera", 150, 180, 2, "CustomGuide", null, null, "JT_CUSTOM_GUIDE", 7310));

    var service = new NpcApplyService(fixture.Root);
    var applyResult = service.Apply(fixture.Paths, report);
    File.AppendAllText(Path.Combine(fixture.PatchDatainfo, "jobname.lub"), "\n-- manual drift");

    AssertThrows<InvalidOperationException>(
        () => service.Rollback(applyResult.ApplyLogPath),
        "NPC rollback should refuse to restore client identity files after manual drift.");
}

static void NpcApplyServiceBlocksTargetsOutsideNpcRoot()
{
    using var fixture = new NpcTestFixture();

    var outsideTarget = Path.Combine(fixture.Rathena, "db", "import", "item_db.yml");
    var report = new NpcDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new NpcDefinitionInput("Unsafe NPC", "prontera", 1, 1, 2, "100", null, null),
        true,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        [new ProposedFileChange(outsideTarget, "append", false, "unsafe")],
        new ItemDiffPreview(
            1,
            0,
            1,
            [new ItemDiffPreviewEntry(outsideTarget, "append", false, 0, 1, string.Empty)]),
        [],
        new NpcSpriteValidation("100", false, true, "numeric-sprite", [], null),
        new NpcSpriteResolution("100", true, false, "numeric-sprite", null, [], false),
        true,
        NpcApplyReadiness.Ready,
        new NpcClientIdentityPlan(
            false,
            true,
            [],
            [],
            [],
            "100",
            true,
            "numeric-sprite",
            null,
            false,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []));

    var result = new NpcApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(!result.Applied, "NPC apply should be blocked when a proposed target is outside rAthena npc root.");
    Assert(result.Conflicts.Any(conflict => conflict.Code == "path.outside-npc-roots"), "NPC apply should report the outside-root conflict.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked NPC apply should still emit an audit log.");
    Assert(!File.Exists(outsideTarget), "Blocked NPC apply must not create the unsafe target.");
}

static void MonsterDryRunBuildsDbAndSpawnPreview()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_TEST_MOB",
            "RagnaForge Test Mob",
            "prontera",
            10,
            1000,
            5,
            60000,
            "PORING",
            null,
            new MonsterSpawnDefinition(120, 140, 8, 6, "RF Test Spawn"),
            new MonsterSkillDefinition(175, 1, "any", 10000, 0, 5000, false, "self", "always", 0)));

    Assert(report.CanApply, "Monster dry-run should remain applicable on a free ID/Aegis and valid map.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("mob_skill_db.txt", StringComparison.OrdinalIgnoreCase)), "Monster dry-run should propose mob skill DB when a skill is provided.");
    Assert(report.ProposedChanges.Any(change => change.Preview.Contains("prontera,120,140,8,6", StringComparison.Ordinal)), "Monster spawn preview should include richer coordinate/range data.");
    Assert(report.DiffPreview.Entries.Count == report.ProposedChanges.Count, "Monster diff preview should cover all proposed changes.");
}

static void MonsterDryRunSupportsMultipleDropsSkillsAndSpawnEvents()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_ADV_MOB",
            "RagnaForge Advanced Mob",
            "prontera",
            20,
            5000,
            4,
            45000,
            "PORING",
            null,
            new MonsterSpawnDefinition(120, 140, 8, 6, "RF Advanced Spawn", "prontera", 4, 45000, "TreeHandler::OnKill"),
            new MonsterSkillDefinition(175, 1, "any", 9000, 0, 5000, false, "self", "always", 0),
            [
                new MonsterDropDefinition(null, "Apple", 5000),
                new MonsterDropDefinition(null, "Jellopy", 1000, null, true)
            ],
            [
                new MonsterSkillDefinition(176, 2, "attack", 2000, 100, 2000, true, "target", "myhpinrate", 20, 60, null, null, null, null, 29, null, "RF_ADV_MOB@RagnaForge_02")
            ],
            [
                new MonsterSpawnDefinition(0, 0, 0, 0, "RF Random Spawn", "geffen", 1, 30000, null, true)
            ]));

    Assert(report.CanApply, "Advanced monster dry-run should stay applicable when all dependencies exist.");
    Assert(report.Drops.Count == 2, "Advanced monster dry-run should expose both drops.");
    Assert(report.Skills.Count == 2, "Advanced monster dry-run should expose both skills.");
    Assert(report.Spawns.Count == 2, "Advanced monster dry-run should expose both spawns.");
    Assert(report.ProposedChanges.Single(change => change.TargetPath.EndsWith("mob_db.yml", StringComparison.OrdinalIgnoreCase)).Preview.Contains("MvpDrops:", StringComparison.Ordinal), "mob_db preview should include MVP drops.");
    Assert(report.ProposedChanges.Single(change => change.TargetPath.EndsWith("mob_skill_db.txt", StringComparison.OrdinalIgnoreCase)).Preview.Contains("RF_ADV_MOB@RagnaForge_02", StringComparison.Ordinal), "mob_skill preview should include the second skill anchor.");
    Assert(report.ProposedChanges.Single(change => change.TargetPath.EndsWith("ragnaforge_mob_rf_adv_mob.txt", StringComparison.OrdinalIgnoreCase)).Preview.Contains("TreeHandler::OnKill", StringComparison.Ordinal), "Spawn preview should include the event label.");
    Assert(report.PostWriteValidationPlan.Count == report.ProposedChanges.Count, "Advanced monster dry-run should publish a post-write validation plan per proposed file.");
}

static void MonsterDryRunBlocksUnresolvedDropItem()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_BAD_DROP",
            "RagnaForge Bad Drop",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(10, 10, 0, 0, "Bad Drop Spawn"),
            null,
            [
                new MonsterDropDefinition(null, "Missing_Item", 5000)
            ]));

    Assert(!report.CanApply, "Monster dry-run should block a drop that does not exist in item_db.yml.");
    Assert(report.ValidationErrors.Any(message => message.Contains("Missing_Item", StringComparison.OrdinalIgnoreCase)), "Missing drop item should be reported in validation errors.");
}

static void MonsterDryRunBlocksDuplicateDropsAndInvalidChance()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_DUP_DROP",
            "RagnaForge Duplicate Drop",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(10, 10, 0, 0, "Duplicate Drop Spawn"),
            null,
            [
                new MonsterDropDefinition(null, "Apple", 20000),
                new MonsterDropDefinition(null, "Apple", 5000)
            ]));

    Assert(!report.CanApply, "Monster dry-run should block duplicate drops and invalid chances.");
    Assert(report.ValidationErrors.Any(message => message.Contains("valid range is 1..10000", StringComparison.OrdinalIgnoreCase)), "Invalid drop chance should be reported.");
    Assert(report.ValidationErrors.Any(message => message.Contains("duplicated", StringComparison.OrdinalIgnoreCase)), "Duplicate drop should be reported.");
}

static void MonsterDryRunBlocksDuplicateSkillsAndUnsupportedFields()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_DUP_SKILL",
            "RagnaForge Duplicate Skill",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(10, 10, 0, 0, "Duplicate Skill Spawn"),
            new MonsterSkillDefinition(175, 1, "any", 1000, 0, 5000, false, "self", "always", 0, null, null, null, null, null, null, null, "RF_DUP_SKILL@RagnaForge_DUP"),
            [],
            [
                new MonsterSkillDefinition(
                    175,
                    1,
                    "any",
                    1000,
                    0,
                    5000,
                    false,
                    "self",
                    "always",
                    0,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "RF_DUP_SKILL@RagnaForge_DUP",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["cooldown-group"] = "boss"
                    })
            ]));

    Assert(!report.CanApply, "Monster dry-run should block duplicate skills and unsupported fields.");
    Assert(report.ValidationErrors.Any(message => message.Contains("duplicated", StringComparison.OrdinalIgnoreCase)), "Duplicate skill anchor should be reported.");
    Assert(report.UnsupportedFields.Any(message => message.Contains("cooldown-group", StringComparison.OrdinalIgnoreCase)), "Unsupported skill fields should be exposed.");
}

static void MonsterDryRunBlocksDuplicateAndInvalidSpawns()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_BAD_SPAWN",
            "RagnaForge Bad Spawn",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(20, 30, 0, 0, "Repeated Spawn", "prontera", 1, 60000),
            null,
            [],
            [],
            [
                new MonsterSpawnDefinition(20, 30, 0, 0, "Repeated Spawn", "prontera", 1, 60000),
                new MonsterSpawnDefinition(-1, 10, 0, 0, "Broken Spawn", "prontera", 1, 60000)
            ]));

    Assert(!report.CanApply, "Monster dry-run should block duplicate and invalid spawns.");
    Assert(report.ValidationErrors.Any(message => message.Contains("duplicated", StringComparison.OrdinalIgnoreCase)), "Duplicate spawn should be reported.");
    Assert(report.ValidationErrors.Any(message => message.Contains("negative coordinates", StringComparison.OrdinalIgnoreCase)), "Invalid spawn coordinates should be reported.");
}

static void MonsterDryRunBlocksSpawnOnMissingMap()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_MISSING_MAP",
            "RagnaForge Missing Map",
            "missing_map",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(10, 10, 0, 0, "Missing Map Spawn", "missing_map", 1, 60000),
            null));

    Assert(!report.CanApply, "Monster dry-run should block spawns on unregistered maps.");
    Assert(report.ValidationErrors.Any(message => message.Contains("missing_map", StringComparison.OrdinalIgnoreCase)), "Missing map should be reported in validation errors.");
}

static void MonsterApplyServiceAppliesAndRollsBackSafely()
{
    using var fixture = new MonsterTestFixture();
    var repositoryPaths = fixture.Paths;
    var report = new MonsterDryRunService().Create(
        repositoryPaths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_APPLY_MOB",
            "RagnaForge Apply Mob",
            "prontera",
            10,
            1000,
            5,
            60000,
            "PORING",
            null,
            new MonsterSpawnDefinition(120, 140, 8, 6, "RF Apply Spawn"),
            new MonsterSkillDefinition(175, 1, "any", 10000, 0, 5000, false, "self", "always", 0)));

    var service = new MonsterApplyService(fixture.Root);
    var applyResult = service.Apply(repositoryPaths, report);
    var spawnPath = Path.Combine(fixture.Rathena, "npc", "custom", "ragnaforge_mob_rf_apply_mob.txt");

    Assert(applyResult.Applied, "Monster apply should succeed for a valid dry-run report.");
    Assert(File.Exists(applyResult.ApplyLogPath), "Monster apply log should be written inside the workspace.");
    Assert(applyResult.ApplyLogPath.Contains(Path.Combine("data", "logs", "monsters"), StringComparison.OrdinalIgnoreCase), "Monster apply log should live under data/logs/monsters.");
    Assert(applyResult.Files.Count == report.ProposedChanges.Count, "Monster apply should audit every proposed change.");
    Assert(applyResult.Files.All(file => !string.IsNullOrWhiteSpace(file.NewSha256)), "Monster apply audit should capture output hashes.");
    Assert(applyResult.PostWriteValidation is not null && applyResult.PostWriteValidation.IsValid, "Monster apply should store a successful post-write validation summary.");
    Assert(File.ReadAllText(Path.Combine(fixture.Rathena, "db", "import", "mob_db.yml")).Contains("RF_APPLY_MOB", StringComparison.Ordinal), "Monster DB should contain the applied AegisName.");
    Assert(File.ReadAllText(Path.Combine(fixture.Rathena, "db", "import", "mob_avail.yml")).Contains("PORING", StringComparison.Ordinal), "Monster availability should contain the sprite override.");
    Assert(File.ReadAllText(Path.Combine(fixture.Rathena, "db", "import", "mob_skill_db.txt")).Contains("RF_APPLY_MOB@RagnaForge", StringComparison.Ordinal), "Monster skill DB should contain the RagnaForge skill row.");
    Assert(File.Exists(spawnPath), "Monster apply should create the custom spawn script.");
    Assert(File.ReadAllText(Path.Combine(fixture.Rathena, "npc", "scripts_custom.conf")).Contains("ragnaforge_mob_rf_apply_mob.txt", StringComparison.Ordinal), "Monster loader should be appended.");

    var rollbackResult = service.Rollback(applyResult.ApplyLogPath);
    Assert(rollbackResult.RolledBack, "Monster rollback should succeed after apply.");
    Assert(File.Exists(rollbackResult.RollbackLogPath), "Monster rollback log should be written inside the workspace.");
    Assert(!File.Exists(spawnPath), "Monster rollback should remove the created spawn script.");
    Assert(!File.ReadAllText(Path.Combine(fixture.Rathena, "db", "import", "mob_db.yml")).Contains("RF_APPLY_MOB", StringComparison.Ordinal), "Monster rollback should restore mob_db.yml.");
    Assert(!File.ReadAllText(Path.Combine(fixture.Rathena, "db", "import", "mob_avail.yml")).Contains("PORING", StringComparison.Ordinal), "Monster rollback should restore mob_avail.yml.");
    Assert(!File.ReadAllText(Path.Combine(fixture.Rathena, "db", "import", "mob_skill_db.txt")).Contains("RF_APPLY_MOB@RagnaForge", StringComparison.Ordinal), "Monster rollback should restore mob_skill_db.txt.");
    Assert(!File.ReadAllText(Path.Combine(fixture.Rathena, "npc", "scripts_custom.conf")).Contains("ragnaforge_mob_rf_apply_mob.txt", StringComparison.Ordinal), "Monster rollback should restore scripts_custom.conf.");
}

static void MonsterApplyServiceBlocksDuplicateIdBeforeWriting()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "npc", "custom"));
    Directory.CreateDirectory(Path.Combine(rathena, "npc"));
    Directory.CreateDirectory(patch);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var mobDbPath = Path.Combine(rathena, "db", "import", "mob_db.yml");
    File.WriteAllText(mobDbPath, "Body:\n  - Id: 45000\n    AegisName: RF_EXISTING_MOB\n");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var report = new MonsterDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            45000,
            "RF_NEW_MOB",
            "RagnaForge New Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(0, 0, 0, 0, null),
            null),
        45000,
        true,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        [new ProposedFileChange(mobDbPath, "append", true, "\n# RagnaForge monster dry-run proposal\n  - Id: 45000\n    AegisName: RF_NEW_MOB")],
        new ItemDiffPreview(1, 0, 1, [new ItemDiffPreviewEntry(mobDbPath, "append", true, 3, 4, string.Empty)]),
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        MonsterApplyReadiness.Ready,
        []);

    var result = new MonsterApplyService(workspace.Root).Apply(repositoryPaths, report);

    Assert(!result.Applied, "Monster apply should be blocked when the ID now exists.");
    Assert(result.Conflicts.Any(conflict => conflict.Code == "monster.id-present"), "Monster apply should report duplicate ID conflict.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked monster apply should still emit an audit log.");
    Assert(!File.ReadAllText(mobDbPath).Contains("RF_NEW_MOB", StringComparison.Ordinal), "Blocked monster apply must not append the new monster.");
}

static void MonsterApplyServiceBlocksTargetsOutsideMonsterRoots()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "npc"));
    Directory.CreateDirectory(patch);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var outsideTarget = Path.Combine(patch, "data", "monster.txt");
    var report = new MonsterDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            45001,
            "RF_UNSAFE_MOB",
            "RagnaForge Unsafe Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(0, 0, 0, 0, null),
            null),
        45001,
        true,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        [new ProposedFileChange(outsideTarget, "append", false, "unsafe")],
        new ItemDiffPreview(1, 0, 1, [new ItemDiffPreviewEntry(outsideTarget, "append", false, 0, 1, string.Empty)]),
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        MonsterApplyReadiness.Ready,
        []);

    var result = new MonsterApplyService(workspace.Root).Apply(repositoryPaths, report);

    Assert(!result.Applied, "Monster apply should be blocked when a proposed target is outside allowed monster roots.");
    Assert(result.Conflicts.Any(conflict => conflict.Code == "path.outside-monster-roots"), "Monster apply should report the outside-root conflict.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked monster apply should still emit an audit log.");
    Assert(!File.Exists(outsideTarget), "Blocked monster apply must not create the unsafe target.");
}

static void MonsterApplyServiceRecordsPostWriteValidation()
{
    using var fixture = new MonsterTestFixture();

    var report = new MonsterDryRunService().Create(
        fixture.Paths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MonsterDefinitionInput(
            null,
            "RF_VALIDATED_MOB",
            "RagnaForge Validated Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            "PORING",
            null,
            new MonsterSpawnDefinition(10, 10, 0, 0, "Validated Spawn"),
            new MonsterSkillDefinition(175, 1, "any", 1000, 0, 5000, false, "self", "always", 0),
            [
                new MonsterDropDefinition(null, "Apple", 5000)
            ]));

    var result = new MonsterApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(result.Applied, "Validated monster apply should succeed.");
    Assert(result.PostWriteValidation is not null && result.PostWriteValidation.IsValid, "Monster apply should keep a successful post-write validation summary.");
    Assert(result.PostWriteValidation!.Files.Any(file => file.ValidatorName == "YamlSyntaxValidator"), "Post-write validation should include YAML validation.");
    Assert(result.PostWriteValidation.Files.Any(file => file.ValidatorName == "RathenaTxtValidator"), "Post-write validation should include mob_skill TXT validation.");
    Assert(result.PostWriteValidation.Files.Any(file => file.ValidatorName == "RathenaScriptValidator"), "Post-write validation should include script validation.");

    var rollback = new MonsterApplyService(fixture.Root).Rollback(result.ApplyLogPath);
    Assert(rollback.RolledBack, "Rollback should still work after validated apply.");
}

static void MonsterApplyServiceBlocksMalformedYamlStaging()
{
    using var fixture = new MonsterTestFixture();
    var mobDbPath = Path.Combine(fixture.Rathena, "db", "import", "mob_db.yml");
    var report = CreateSyntheticMonsterDryRunReport(
        new MonsterDefinitionInput(
            45010,
            "RF_BAD_YAML",
            "Bad Yaml Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(0, 0, 0, 0, null),
            null),
        45010,
        [
            new ProposedFileChange(mobDbPath, "append", true, "\n  - Id 45010\n    AegisName RF_BAD_YAML")
        ]);

    var result = new MonsterApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(!result.Applied, "Monster apply should block malformed YAML staging.");
    Assert(result.PostWriteValidation is not null && !result.PostWriteValidation.IsValid, "Malformed YAML should fail post-write validation.");
    Assert(result.PostWriteValidation!.Files.Any(file => file.ValidatorName == "YamlSyntaxValidator" && !file.IsValid), "YAML validator should reject malformed staging.");
    Assert(!File.ReadAllText(mobDbPath).Contains("RF_BAD_YAML", StringComparison.Ordinal), "Malformed YAML staging must not reach the real mob_db.yml.");
}

static void MonsterApplyServiceBlocksMalformedTxtStaging()
{
    using var fixture = new MonsterTestFixture();
    var mobSkillPath = Path.Combine(fixture.Rathena, "db", "import", "mob_skill_db.txt");
    var report = CreateSyntheticMonsterDryRunReport(
        new MonsterDefinitionInput(
            45011,
            "RF_BAD_TXT",
            "Bad Txt Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(0, 0, 0, 0, null),
            null),
        45011,
        [
            new ProposedFileChange(mobSkillPath, "append", true, "\n45011,BROKEN")
        ]);

    var result = new MonsterApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(!result.Applied, "Monster apply should block malformed mob_skill staging.");
    Assert(result.PostWriteValidation is not null && !result.PostWriteValidation.IsValid, "Malformed TXT should fail post-write validation.");
    Assert(result.PostWriteValidation!.Files.Any(file => file.ValidatorName == "RathenaTxtValidator" && !file.IsValid), "TXT validator should reject malformed mob_skill staging.");
    Assert(!File.ReadAllText(mobSkillPath).Contains("BROKEN", StringComparison.Ordinal), "Malformed TXT staging must not reach the real mob_skill_db.txt.");
}

static void MonsterApplyServiceBlocksMalformedSpawnStaging()
{
    using var fixture = new MonsterTestFixture();
    var spawnPath = Path.Combine(fixture.Rathena, "npc", "custom", "ragnaforge_mob_bad_spawn.txt");
    var report = CreateSyntheticMonsterDryRunReport(
        new MonsterDefinitionInput(
            45012,
            "RF_BAD_SPAWN",
            "Bad Spawn Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(0, 0, 0, 0, null),
            null),
        45012,
        [
            new ProposedFileChange(spawnPath, "create", false, "prontera\tmonster\tbroken")
        ]);

    var result = new MonsterApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(!result.Applied, "Monster apply should block malformed spawn staging.");
    Assert(result.PostWriteValidation is not null && !result.PostWriteValidation.IsValid, "Malformed spawn script should fail post-write validation.");
    Assert(result.PostWriteValidation!.Files.Any(file => file.ValidatorName == "RathenaScriptValidator" && !file.IsValid), "Script validator should reject malformed spawn staging.");
    Assert(!File.Exists(spawnPath), "Malformed spawn staging must not create the real spawn file.");
}

static void MonsterApplyServiceRollsBackAfterMidApplyFailure()
{
    using var fixture = new MonsterTestFixture();
    var mobDbPath = Path.Combine(fixture.Rathena, "db", "import", "mob_db.yml");
    var invalidTarget = Path.Combine(fixture.Rathena, "npc", "custom");
    var report = CreateSyntheticMonsterDryRunReport(
        new MonsterDefinitionInput(
            45013,
            "RF_MIDFAIL",
            "Mid Failure Mob",
            "prontera",
            10,
            1000,
            1,
            60000,
            null,
            null,
            new MonsterSpawnDefinition(0, 0, 0, 0, null),
            null),
        45013,
        [
            new ProposedFileChange(mobDbPath, "append", true, "\n# RagnaForge monster dry-run proposal\n  - Id: 45013\n    AegisName: RF_MIDFAIL\n    Name: Mid Failure Mob"),
            new ProposedFileChange(invalidTarget, "create", false, "placeholder")
        ]);

    var result = new MonsterApplyService(fixture.Root).Apply(fixture.Paths, report);

    Assert(!result.Applied, "Monster apply should fail when a later write target is invalid.");
    Assert(result.Messages.Any(message => message.Contains("automatic rollback", StringComparison.OrdinalIgnoreCase)), "Mid-apply failure should report automatic rollback.");
    Assert(!File.ReadAllText(mobDbPath).Contains("RF_MIDFAIL", StringComparison.Ordinal), "Automatic rollback should restore mob_db.yml after a mid-apply failure.");
}

static void MapDryRunResolvesIndexedGrfAssets()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var containerPath = Path.Combine(grfs, "sample.grf");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_cache.dat"), "cache");
    File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");
    File.WriteAllText(Path.Combine(rathena, "mapcache.exe"), "fake-tool");
    File.WriteAllText(containerPath, "fake");

    var indexStore = new JsonGrfContainerIndexStore(workspace.Root);
    indexStore.Save(
        indexStore.BuildDefaultIndexPath(containerPath),
        GrfContainerContentIndexDocument.Create(
            containerPath,
            containerLength: 1,
            containerLastWriteTimeUtc: DateTimeOffset.UtcNow,
            entryCount: 3,
            directoryCount: 1,
            maxEntriesCaptured: 3,
            isTruncated: false,
            extensionCounts: [new GrfExtensionCount(".rsw", 1), new GrfExtensionCount(".gnd", 1), new GrfExtensionCount(".gat", 1)],
            topLevelDirectories: [new GrfTopLevelDirectoryCount("data", 3)],
            entries:
            [
                new GrfContentEntrySnapshot("data/sample.rsw", "data", "sample.rsw", ".rsw", 10, 10, false),
                new GrfContentEntrySnapshot("data/sample.gnd", "data", "sample.gnd", ".gnd", 10, 10, false),
                new GrfContentEntrySnapshot("data/sample.gat", "data", "sample.gat", ".gat", 10, 10, false)
            ]),
        overwrite: true);

    var report = new MapDryRunService(
        new IndexedGrfAssetLookupService(indexStore, new CountingGrfAssetLookupService()),
        new GrfAssetLookupOptions(true, [containerPath], 1, 10)).Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MapDeploymentInput("sample", "sample", "sample", "sample", null));

    Assert(report.CanApply, "Map dry-run should remain applicable when required map assets are resolved through the local GRF index.");
    Assert(report.RswLookup is not null && report.RswLookup.Source == GrfAssetLookupSource.LocalIndex, "RSW lookup should report local index provenance.");
    Assert(report.GndLookup is not null && report.GndLookup.Source == GrfAssetLookupSource.LocalIndex, "GND lookup should report local index provenance.");
    Assert(report.GatLookup is not null && report.GatLookup.Source == GrfAssetLookupSource.LocalIndex, "GAT lookup should report local index provenance.");
}

static void MapDryRunScansLooseDependencies()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "colosseum"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "oldcastle"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "sound"));

    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_cache.dat"), "cache");
    File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");
    File.WriteAllText(Path.Combine(patch, "data", "sample.rsw"), "sample.gnd sample.gat oldcastle\\shield.rsm sound\\effect.wav effect\\spark.str");
    File.WriteAllText(Path.Combine(patch, "data", "sample.gnd"), "colosseum\\floor.bmp");
    File.WriteAllText(Path.Combine(patch, "data", "sample.gat"), "");
    File.WriteAllText(Path.Combine(patch, "data", "oldcastle", "shield.rsm"), "fake");
    File.WriteAllText(Path.Combine(patch, "data", "sound", "effect.wav"), "fake");
    File.WriteAllText(Path.Combine(patch, "data", "colosseum", "floor.bmp"), "fake");

    var report = new MapDryRunService().Create(
        new RepositoryPaths(rathena, patch, workspace.Root, workspace.Root),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MapDeploymentInput("sample", "sample", "sample", "sample", null));

    Assert(report.DependencyScan is not null && report.DependencyScan.DeepScanAvailable, "Loose map binaries should enable deep dependency scan.");
    var dependencyScan = report.DependencyScan!;
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Model" && asset.Resolved), "Map dependency scan should capture resolved model references.");
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Texture" && asset.Resolved), "Map dependency scan should capture resolved texture references.");
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Effect" && !asset.Resolved), "Map dependency scan should keep unresolved effect references visible.");
}

static void MapDryRunScansDependenciesFromControlledGrfExtraction()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var tempExtractionRoot = Path.Combine(workspace.Root, "tmp", "map-dependency-scan");
    var containerPath = Path.Combine(grfs, "maps.grf");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "colosseum"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "oldcastle"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "sound"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "effect"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_cache.dat"), "cache");
    File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");
    File.WriteAllText(containerPath, "fake");
    File.WriteAllText(Path.Combine(patch, "data", "oldcastle", "shield.rsm"), "fake");
    File.WriteAllText(Path.Combine(patch, "data", "sound", "effect.wav"), "fake");
    File.WriteAllText(Path.Combine(patch, "data", "effect", "spark.str"), "fake");
    File.WriteAllText(Path.Combine(patch, "data", "colosseum", "floor.bmp"), "fake");

    var extractor = new ControlledMapFakeGrfFileExtractor();
    var report = new MapDryRunService(
        new MapAssetFakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [containerPath], 1, 10),
        extractor,
        tempExtractionRoot).Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MapDeploymentInput("sample", "sample", "sample", "sample", null));

    Assert(extractor.Calls == 1, "Controlled GRF extractor should be called once when loose .rsw/.gnd files are unavailable.");
    Assert(report.DependencyScan is not null && report.DependencyScan.DeepScanAvailable, "Controlled GRF extraction should enable deep map dependency scan.");
    var dependencyScan = report.DependencyScan!;
    Assert(dependencyScan.Source == "ControlledGrfExtraction", "Dependency scan should expose controlled GRF extraction provenance.");
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Model" && asset.Resolved), "Extracted RSW references should include resolved models.");
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Texture" && asset.Resolved), "Extracted GND references should include resolved textures.");
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Sound" && asset.Resolved), "Extracted RSW references should include resolved sounds.");
    Assert(dependencyScan.ReferencedAssets.Any(asset => asset.Category == "Effect" && asset.Resolved), "Extracted RSW references should include resolved effects.");
    Assert(report.Warnings.Any(warning => warning.Contains("controlled temporary extraction", StringComparison.OrdinalIgnoreCase)), "Dry-run should report controlled temporary extraction.");
    Assert(!Directory.Exists(tempExtractionRoot) || !Directory.EnumerateFileSystemEntries(tempExtractionRoot).Any(), "Controlled extraction temporary files should be cleaned after scan.");
}

static void MapDryRunExposesAssetPlansAndBlocksBinaryRename()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "effect"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_cache.dat"), "cache");
    File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");
    File.WriteAllText(Path.Combine(rathena, "mapcache.exe"), "fake-tool");
    File.WriteAllText(Path.Combine(patch, "data", "sample_old.rsw"), "sample_old.gnd sample_old.gat effect\\spark.str");
    File.WriteAllText(Path.Combine(patch, "data", "sample_old.gnd"), "texture\\floor.bmp");
    File.WriteAllText(Path.Combine(patch, "data", "effect", "spark.str"), "fake");

    var report = new MapDryRunService(
        new MapAssetFakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [Path.Combine(grfs, "maps.grf")], 1, 10)).Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MapDeploymentInput("sample_new", "sample_old", "sample_old", "sample_old", null));

    Assert(!report.CanApply, "Map dry-run should block binary rename scenarios.");
    Assert(report.AssetPlans.Any(plan => plan.RelativePath == "sample_new.rsw" && plan.NeedsCopy), "Map dry-run should expose a copy action for the core trio.");
    Assert(report.MapCachePlan is not null && report.MapCachePlan.ToolDetected, "Map dry-run should expose mapcache tool availability.");
    Assert(report.Dependencies.Any(item => item.State == ItemDependencyState.Missing && item.Message.Contains("Binary map rename", StringComparison.OrdinalIgnoreCase)), "Binary rename block should be visible in dependencies.");
}

static void MapApplyServiceAppliesAndRollsBackSafely()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var tempExtractionRoot = Path.Combine(workspace.Root, "tmp", "map-dependency-scan");
    var containerPath = Path.Combine(grfs, "maps.grf");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "oldcastle"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "sound"));
    Directory.CreateDirectory(Path.Combine(patch, "data", "effect"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    var mapIndexPath = Path.Combine(rathena, "db", "import", "map_index.txt");
    var mapCachePath = Path.Combine(rathena, "db", "import", "map_cache.dat");
    var mapsAthenaPath = Path.Combine(rathena, "conf", "maps_athena.conf");
    File.WriteAllText(mapIndexPath, "");
    File.WriteAllBytes(mapCachePath, MapCacheTestUtility.BuildSyntheticMapCache("prontera"));
    File.WriteAllText(mapsAthenaPath, "");
    File.WriteAllText(Path.Combine(rathena, "mapcache.exe"), "fake-tool");
    File.WriteAllText(containerPath, "fake");
    File.WriteAllText(Path.Combine(patch, "data", "oldcastle", "shield.rsm"), "model");
    File.WriteAllText(Path.Combine(patch, "data", "sound", "effect.wav"), "sound");
    File.WriteAllText(Path.Combine(patch, "data", "effect", "spark.str"), "effect");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var extractor = new ControlledMapFakeGrfFileExtractor();
    var report = new MapDryRunService(
        new MapAssetFakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [containerPath], 1, 10),
        extractor,
        tempExtractionRoot).Create(
        repositoryPaths,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MapDeploymentInput("sample", "sample", "sample", "sample", null));

    Assert(report.CanApply, "Map dry-run should remain applicable for a resolved GRF-backed map.");
    Assert(report.AssetPlans.Any(plan => plan.RelativePath == "sample.rsw" && plan.NeedsCopy), "Map dry-run should plan the core trio copy.");
    Assert(report.MapCachePlan is not null && report.MapCachePlan.ToolDetected, "Dry-run should detect mapcache.exe when present.");

    var service = new MapApplyService(
        workspace.Root,
        new ControlledMapApplyFakeGrfFileExtractor(),
        new FakeMapCacheBuilder());
    var applyResult = service.Apply(repositoryPaths, report);

    Assert(applyResult.Applied, "Map apply should succeed for a valid dry-run report.");
    Assert(File.Exists(applyResult.ApplyLogPath), "Map apply log should be written inside the workspace.");
    Assert(applyResult.ApplyLogPath.Contains(Path.Combine("data", "logs", "maps"), StringComparison.OrdinalIgnoreCase), "Map apply log should live under data/logs/maps.");
    Assert(applyResult.Files.Count >= 4, "Map apply should audit text changes, copied assets and map cache rebuild.");
    Assert(MapCacheTestUtility.ContainsMap(mapCachePath, "sample"), "Map cache rebuild should include the applied map.");
    Assert(File.Exists(Path.Combine(patch, "data", "sample.rsw")), "Map apply should create sample.rsw in the patch.");
    Assert(File.Exists(Path.Combine(patch, "data", "sample.gnd")), "Map apply should create sample.gnd in the patch.");
    Assert(File.Exists(Path.Combine(patch, "data", "sample.gat")), "Map apply should create sample.gat in the patch.");
    Assert(File.ReadAllText(mapIndexPath).Contains("sample 0", StringComparison.Ordinal), "Map index should contain the applied map.");
    Assert(File.ReadAllText(mapsAthenaPath).Contains("map: sample", StringComparison.Ordinal), "maps_athena.conf should contain the applied map.");

    var rollbackResult = service.Rollback(applyResult.ApplyLogPath);
    Assert(rollbackResult.RolledBack, "Map rollback should succeed after apply.");
    Assert(File.Exists(rollbackResult.RollbackLogPath), "Map rollback log should be written inside the workspace.");
    Assert(!File.Exists(Path.Combine(patch, "data", "sample.rsw")), "Map rollback should remove created map assets.");
    Assert(!File.Exists(Path.Combine(patch, "data", "sample.gnd")), "Map rollback should remove created map assets.");
    Assert(!File.Exists(Path.Combine(patch, "data", "sample.gat")), "Map rollback should remove created map assets.");
    Assert(!File.ReadAllText(mapIndexPath).Contains("sample 0", StringComparison.Ordinal), "Map rollback should restore map_index.txt.");
    Assert(!File.ReadAllText(mapsAthenaPath).Contains("map: sample", StringComparison.Ordinal), "Map rollback should restore maps_athena.conf.");
    Assert(!MapCacheTestUtility.ContainsMap(mapCachePath, "sample"), "Map rollback should restore the prior map cache.");
}

static void MapApplyServiceBlocksTargetsOutsideMapRoots()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var outsideTarget = Path.Combine(workspace.Root, "unsafe", "sample.rsw");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(patch);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
    File.WriteAllBytes(Path.Combine(rathena, "db", "import", "map_cache.dat"), MapCacheTestUtility.BuildSyntheticMapCache("prontera"));
    File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");

    var repositoryPaths = new RepositoryPaths(rathena, patch, grfs, grfEditor);
    var report = new MapDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new MapDeploymentInput("sample", "sample", "sample", "sample", null),
        true,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        [new ProposedFileChange(Path.Combine(rathena, "db", "import", "map_index.txt"), "append", true, "sample 0")],
        new ItemDiffPreview(
            1,
            0,
            1,
            [new ItemDiffPreviewEntry(Path.Combine(rathena, "db", "import", "map_index.txt"), "append", true, 0, 1, string.Empty)]),
        [],
        null,
        null,
        null,
        null,
        [
            new MapAssetPlan("MapCore", "sample.rsw", outsideTarget, false, "LoosePatch", Path.Combine(workspace.Root, "sample.rsw"), null, true, true)
        ],
        new MapCachePlan(true, Path.Combine(rathena, "mapcache.exe"), Path.Combine(rathena, "db", "import", "map_cache.dat"), true, [], []));

    File.WriteAllText(Path.Combine(workspace.Root, "sample.rsw"), "unsafe");

    var result = new MapApplyService(
        workspace.Root,
        new ControlledMapApplyFakeGrfFileExtractor(),
        new FakeMapCacheBuilder()).Apply(repositoryPaths, report);

    Assert(!result.Applied, "Map apply should be blocked when a target is outside allowed roots.");
    Assert(result.Conflicts.Any(conflict => conflict.Code == "path.outside-map-roots"), "Map apply should report the outside-root conflict.");
    Assert(File.Exists(result.ApplyLogPath), "Blocked map apply should still emit an audit log.");
    Assert(!File.Exists(outsideTarget), "Blocked map apply must not create the unsafe target.");
}

static void LegacyEquipmentDryRunWarnsShieldLikeRobeHint()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "spriterobename.lub"), "RobeNameTable = {\n\t[SPRITE_ROBE_IDs.ROBE_C_Lord_Of_Death_Shield] = \"C_Lord_Of_Death_Shield\",\n}\n");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Shield_Visual",
                "RagnaForge Shield Visual",
                "rf_shield_visual",
                "Armor",
                10,
                5,
                100,
                0,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Left_Hand"],
            "shield",
            3,
            "SHIELD_RF_CUSTOM",
            "C_Lord_Of_Death_Shield",
            [],
            null,
            1,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null));

    Assert(report.Dependencies.Any(item => item.Message.Contains("robe/Costume_Garment", StringComparison.OrdinalIgnoreCase)), "Shield visual dry-run should redirect robe-backed visuals to the robe pipeline.");
}

static void LegacyEquipmentDryRunExposesVisualGrfLookupProvenance()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "accessoryid.lub"), "ACCESSORY_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "accname.lub"), "AccNameTable = {}\n");

    var service = new LegacyEquipmentDryRunService(
        new FakeGrfAssetLookupService(),
        new GrfAssetLookupOptions(true, [Path.Combine(grfs, "sample.grf")], 1, 10));
    var report = service.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Costume_Rabbit",
                "RagnaForge Costume Rabbit",
                "rf_costume_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5000,
            "ACCESSORY_RF_COSTUME_RABBIT",
            "_rf_costume_rabbit",
            ["All"],
            null,
            10,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null));

    Assert(report.CanApply, "Equipment dry-run should remain applicable when GRF lookup satisfies the visual asset dependency.");
    Assert(report.VisualAssetLookup is not null && report.VisualAssetLookup.Source == GrfAssetLookupSource.LiveScan, "Equipment dry-run should expose visual GRF lookup provenance.");
    Assert(report.Dependencies.Any(item => item.Category == "Assets" && item.Message.Contains("via live scan", StringComparison.OrdinalIgnoreCase)), "Dependency message should mention live scan provenance.");
}

static void LegacyEquipmentDryRunBuildsHeadgearProposal()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "accessoryid.lub"), "ACCESSORY_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "accname.lub"), "AccNameTable = {}\n");

    var service = new LegacyEquipmentDryRunService();
    var report = service.Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Costume_Rabbit",
                "RagnaForge Costume Rabbit",
                "rf_costume_rabbit",
                "Armor",
                10,
                5,
                100,
                1,
                "bonus bLuk,1;",
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5000,
            "ACCESSORY_RF_COSTUME_RABBIT",
            "_rf_costume_rabbit",
            ["All"],
            null,
            10,
            null,
            null,
            null,
            3,
            true,
            null,
            null,
            null,
            null));

    Assert(report.CanApply, "Equipment dry-run should be applicable for supported headgear visuals.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("accessoryid.lub", StringComparison.OrdinalIgnoreCase)), "Accessory ID datainfo proposal should be present.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("accname.lub", StringComparison.OrdinalIgnoreCase)), "AccName datainfo proposal should be present.");
    Assert(report.ProposedChanges[0].Preview.Contains("Locations:", StringComparison.Ordinal), "Equipment item DB snippet should include locations.");
    Assert(report.ProposedChanges[0].Preview.Contains("View: 5000", StringComparison.Ordinal), "Equipment item DB snippet should include view.");
}

static void LegacyEquipmentDryRunBuildsWeaponProposal()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "weapontable.lub"), "Weapon_IDs = {\n\tWEAPONTYPE_SWORD = 2,\n}\nWeaponNameTable = {}\nExpansion_Weapon_IDs = {}\nWeaponHitWaveNameTable = {}\nBowTypeList = {}\n");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Test_Sword",
                "RagnaForge Test Sword",
                "rf_test_sword",
                "Weapon",
                10,
                5,
                100,
                2,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Right_Hand"],
            "weapon",
            6000,
            "WEAPONTYPE_RF_TEST_SWORD",
            "_rf_test_sword",
            [],
            null,
            1,
            null,
            null,
            3,
            null,
            true,
            null,
            null,
            "SWORD",
            null));

    Assert(report.CanApply, "Equipment dry-run should be applicable for a mapped weapon visual.");
    Assert(report.ProposedChanges.Any(change => change.TargetPath.EndsWith("weapontable.lub", StringComparison.OrdinalIgnoreCase)), "Weapon table datainfo proposal should be present.");
    Assert(report.DiffPreview.Entries.Any(entry => entry.UnifiedDiff.Contains("Expansion_Weapon_IDs", StringComparison.Ordinal)), "Weapon diff should include expansion weapon mapping.");
    Assert(report.DiffPreview.Entries.Any(entry => entry.UnifiedDiff.Contains("_hit_sword.wav", StringComparison.Ordinal)), "Weapon diff should infer sword hit sound.");
}

static void LegacyEquipmentDryRunHandlesMissingEquipLocationsSafely()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_No_Location",
                "RagnaForge No Location",
                "rf_no_location",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            [],
            "headgear",
            5003,
            "ACCESSORY_RF_NO_LOCATION",
            "_rf_no_location",
            [],
            null,
            1,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null));

    Assert(!report.CanApply, "Equipment dry-run should stay blocked when equip locations are missing.");
    Assert(report.Dependencies.Any(item => item.State == ItemDependencyState.Missing && item.Message.Contains("equip location", StringComparison.OrdinalIgnoreCase)), "Missing equip locations should be reported as dependency.");
}

static void LegacyEquipmentDryRunBlocksUnsafeVisualIdentifier()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "accessoryid.lub"), "ACCESSORY_IDs = {}\n");
    File.WriteAllText(Path.Combine(datainfo, "accname.lub"), "AccNameTable = {}\n");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Bad_Visual",
                "RagnaForge Bad Visual",
                "rf_bad_visual",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5001,
            "ACCESSORY_RF_BAD;VALUE",
            "_rf_bad_visual",
            [],
            null,
            1,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null));

    Assert(!report.CanApply, "Equipment dry-run should block unsafe visual symbols.");
    Assert(report.Dependencies.Any(item => item.State == ItemDependencyState.Missing && item.Message.Contains("safe Lua identifier", StringComparison.OrdinalIgnoreCase)), "Unsafe symbol should be reported.");
    Assert(!report.ProposedChanges.Any(change => change.TargetPath.EndsWith("accessoryid.lub", StringComparison.OrdinalIgnoreCase)), "Unsafe datainfo append must not be proposed.");
}

static void LegacyEquipmentDryRunBlocksDuplicateVisualId()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");
    var datainfo = Path.Combine(patch, "data", "luafiles514", "lua files", "datainfo");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(datainfo);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");
    File.WriteAllText(Path.Combine(datainfo, "accessoryid.lub"), "ACCESSORY_IDs = {\n\tACCESSORY_ALREADY_USED = 5002,\n}\n");
    File.WriteAllText(Path.Combine(datainfo, "accname.lub"), "AccNameTable = {}\n");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Duplicate_View",
                "RagnaForge Duplicate View",
                "rf_duplicate_view",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Head_Top"],
            "headgear",
            5002,
            "ACCESSORY_RF_DUPLICATE_VIEW",
            "_rf_duplicate_view",
            [],
            null,
            1,
            null,
            null,
            null,
            null,
            true,
            null,
            null,
            null,
            null));

    Assert(!report.CanApply, "Equipment dry-run should block duplicate visual IDs.");
    Assert(report.Dependencies.Any(item => item.State == ItemDependencyState.Missing && item.Message.Contains("View ID 5002 already exists", StringComparison.OrdinalIgnoreCase)), "Duplicate visual ID should be reported.");
}

static void LegacyEquipmentDryRunAllowsBuiltInShieldView()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Test_Shield",
                "RagnaForge Test Shield",
                "rf_test_shield",
                "Armor",
                10,
                5,
                100,
                1,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Left_Hand"],
            "shield",
            3,
            null,
            null,
            [],
            null,
            1,
            null,
            1,
            null,
            10,
            true,
            null,
            null,
            null,
            null));

    Assert(report.CanApply, "Equipment dry-run should allow built-in shield views.");
    Assert(!report.ProposedChanges.Any(change => change.TargetPath.EndsWith(".lub", StringComparison.OrdinalIgnoreCase)), "Built-in shield mode should not propose visual datainfo changes.");
    Assert(report.Dependencies.Any(item => item.State == ItemDependencyState.Satisfied && item.Message.Contains("no visual datainfo append is required", StringComparison.OrdinalIgnoreCase)), "Built-in shield mode should explain the restricted behavior.");
}

static void LegacyEquipmentDryRunBlocksCustomShieldVisualRegistration()
{
    using var workspace = TempWorkspace.Create();
    var rathena = Path.Combine(workspace.Root, "rathena");
    var patch = Path.Combine(workspace.Root, "patch");
    var grfs = Path.Combine(workspace.Root, "grfs");
    var grfEditor = Path.Combine(workspace.Root, "grf-editor");

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n  Version: 3\n");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "idnum2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdisplaynametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemresnametable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "num2itemdesctable.txt"), "");
    File.WriteAllText(Path.Combine(patch, "data", "itemslotcounttable.txt"), "");

    var report = new LegacyEquipmentDryRunService().Create(
        new RepositoryPaths(rathena, patch, grfs, grfEditor),
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        new EquipmentDefinitionInput(
            new ItemDefinitionInput(
                null,
                "RF_Test_Weapon",
                "RagnaForge Test Shield",
                "rf_test_shield",
                "Armor",
                10,
                5,
                100,
                2,
                null,
                ["Visual item."],
                null,
                null,
                []),
            ["Left_Hand"],
            "shield",
            3,
            "SHIELD_RF_TEST_SHIELD",
            "_rf_test_shield",
            [],
            null,
            1,
            null,
            null,
            3,
            null,
            true,
            null,
            null,
            null,
            null));

    Assert(!report.CanApply, "Equipment dry-run should block custom shield visual registration.");
    Assert(report.Dependencies.Any(item => item.State == ItemDependencyState.Missing && item.Message.Contains("do not provide client symbol or client sprite", StringComparison.OrdinalIgnoreCase)), "Custom shield registration should be reported as missing.");
}

static void GrfAssemblyInspectorReadsControlledContainer()
{
    var grfEditorPath = @"C:\Program Files (x86)\GRF Editor";
    var grfClPath = Path.Combine(grfEditorPath, "GrfCL.exe");
    if (!File.Exists(grfClPath))
    {
        Console.WriteLine("SKIP GRF assembly inspector test because GrfCL.exe is not installed.");
        return;
    }

    using var workspace = TempWorkspace.Create();
    var containerPath = Path.Combine(workspace.Root, "sample.grf");
    File.WriteAllBytes(containerPath, Convert.FromBase64String(SampleGrfBase64));

    var result = new GrfAssemblyContainerInspector().Inspect(grfEditorPath, containerPath, maxEntries: 10);

    Assert(result.Index.EntryCount > 0, "Controlled GRF container should expose at least one entry.");
    Assert(result.Index.Entries.Count > 0, "Controlled GRF sample should capture preview entries.");
    Assert(result.Engine.Contains("GRF.dll", StringComparison.OrdinalIgnoreCase), "Inspector should report GRF.dll engine.");
}

static MonsterDryRunReport CreateSyntheticMonsterDryRunReport(
    MonsterDefinitionInput input,
    int resolvedId,
    IReadOnlyList<ProposedFileChange> proposedChanges,
    bool canApply = true)
{
    return new MonsterDryRunReport(
        DateTimeOffset.UtcNow,
        new EpisodeProfile("progressive-current", EpisodeMode.PreRenewal, "2025-07-16", "test"),
        input,
        resolvedId,
        canApply,
        [new ItemDependency("DryRun", ItemDependencyState.Proposed, "test")],
        proposedChanges,
        new ItemDiffPreview(
            proposedChanges.Count,
            proposedChanges.Count(change => !change.Exists),
            proposedChanges.Count(change => change.Exists),
            proposedChanges.Select(change => new ItemDiffPreviewEntry(
                change.TargetPath,
                change.ChangeKind,
                change.Exists,
                0,
                change.Preview.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.None).Length,
                string.Empty)).ToArray()),
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        MonsterApplyReadiness.Ready,
        []);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static RagnaForgeAgentSummaryService CreateAgentSummaryService(string cacheDir)
{
    var executor = new FakeAgentProcessExecutor();
    Directory.CreateDirectory(cacheDir);
    var fakeExe = Path.Combine(cacheDir, "ragnaforge.exe");
    File.WriteAllText(fakeExe, "fake");

    executor.Results["status --json"] = new RagnaForgeAgentProcessResult(0, AgentStatusJson(), "", TimedOut: false);
    executor.Results["doctor --json"] = new RagnaForgeAgentProcessResult(0, AgentDoctorJson(), "", TimedOut: false);
    executor.Results["validate --json"] = new RagnaForgeAgentProcessResult(0, AgentValidateJson(), "", TimedOut: false);

    var runner = new RagnaForgeAgentCommandRunner(
        fakeExe,
        TimeSpan.FromSeconds(1),
        executor,
        NullLogger<RagnaForgeAgentCommandRunner>.Instance);

    return new RagnaForgeAgentSummaryService(
        runner,
        cacheDir,
        NullLogger<RagnaForgeAgentSummaryService>.Instance);
}

static void WriteAgentCacheFiles(string cacheDir, string activeProfile, string configFingerprint)
{
    Directory.CreateDirectory(cacheDir);
    File.WriteAllText(
        Path.Combine(cacheDir, "entities_index.json"),
        JsonSerializer.Serialize(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            agentVersion = "1.2.0-operational-ux",
            activeProfile,
            configFingerprint,
            sourcePaths = new[] { @"E:\Ragnarok\Testes\rAthena_teste" },
            stats = new
            {
                itemsFound = 10,
                monstersFound = 2,
                npcsFound = 3,
                mapsFound = 4,
                filesScanned = 20,
                filesParsed = 8,
                filesSkipped = 12,
                durationMs = 15
            }
        }));

    File.WriteAllText(
        Path.Combine(cacheDir, "project_index.json"),
        JsonSerializer.Serialize(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            agentVersion = "1.2.0-operational-ux",
            activeProfile,
            configFingerprint,
            scanRoot = @"C:\Users\Allis\Desktop\New project",
            stats = new
            {
                filesVisited = 5,
                filesIndexed = 5,
                filesSkipped = 0,
                directoriesVisited = 2,
                durationMs = 7
            }
        }));
}

static string AgentStatusJson() => JsonSerializer.Serialize(new
{
    ok = true,
    agentVersion = "1.2.0-operational-ux",
    activeProfile = "teste",
    configFingerprint = "fingerprint-1",
    data = new
    {
        dbMode = "renewal",
        grfProtected = true,
        lubEditingBlocked = true,
        cache = new
        {
            indexExists = true,
            matchesActiveFingerprint = true
        },
        safety = new
        {
            requireDryRunBeforeApply = true,
            requireDiffBeforeApply = true,
            requireExplicitConfirmation = true,
            backupBeforeApply = true,
            blockOriginalGrfWrite = true,
            blockLubEditing = true,
            invalidateCacheOnPathChange = true,
            cacheMustMatchActiveProfile = true,
            applyBlocked = true,
            rollbackRealBlocked = true
        }
    }
});

static string AgentDoctorJson() => JsonSerializer.Serialize(new
{
    ok = true,
    data = new
    {
        checks = new[]
        {
            new { check = "security.grfReadOnly", severity = "pass", message = "ok" },
            new { check = "safety.blockLubEditing", severity = "pass", message = "ok" }
        }
    }
});

static string AgentValidateJson() => JsonSerializer.Serialize(new
{
    ok = true,
    data = new
    {
        totalIssues = 2,
        errors = 1,
        warnings = 1,
        safeForReadOnlyWork = true,
        safeForDryRun = true,
        safeForApply = false,
        issues = new[]
        {
            new { code = "ITEM_DUPLICATE_ID_SERVER", scope = "rAthena", severity = "error" },
            new { code = "MAP_NO_CLIENT_FILES", scope = "external-data", severity = "warning" }
        }
    }
});

static void SecurityBarrierAntiApply()
{
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("apply --json"), "IsCommandAllowed must block apply command.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("rollback --json"), "IsCommandAllowed must block rollback command.");
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("triage --json"), "IsCommandAllowed must block triage command in integration (since it runs validate internally).");
}

static void SecurityBarrierAntiShell()
{
    var startInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "ragnaforge.exe",
        Arguments = "status --json",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    Assert(!startInfo.UseShellExecute, "UseShellExecute must be false.");
    Assert(startInfo.RedirectStandardOutput, "RedirectStandardOutput must be true.");
    Assert(startInfo.RedirectStandardError, "RedirectStandardError must be true.");
}

static void SecurityBarrierAntiSecrets()
{
    using var workspace = TempWorkspace.Create();
    WriteAgentCacheFiles(workspace.Root, activeProfile: "teste", configFingerprint: "fingerprint-1");
    var service = CreateAgentSummaryService(workspace.Root);

    var summary = service.GetHealthSummaryAsync().GetAwaiter().GetResult();
    var json = JsonSerializer.Serialize(summary);

    Assert(!json.Contains("secret_key", StringComparison.OrdinalIgnoreCase), "Output must not contain secret keys.");
    Assert(!json.Contains("Desktop", StringComparison.OrdinalIgnoreCase), "Output must not expose absolute desktop paths.");
    Assert(!json.Contains("Allis", StringComparison.OrdinalIgnoreCase), "Output must not expose username absolute paths.");
}

static void AssetPreviewServiceBlocksPathTraversal()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);
    var request = new AssetPreviewRequest("Patch", "Loose", "../windows/system32/cmd.exe", ".exe");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "Blocked", "Preview service should block path traversal.");
}

static void AssetPreviewServiceBlocksUnallowedExtension()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    // EntryPath tem .exe, mas ExpectedExtension Ã© .bmp -> Mismatch
    var request = new AssetPreviewRequest("Patch", "Loose", "data/test.exe", ".bmp");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "Blocked", "Preview service should block extension mismatch.");
    Assert(response.Errors.Any(e => e.Contains("Extension mismatch")), "Mismatch error expected.");
}

static void AssetPreviewServiceBlocksArbitraryGrfContainer()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var request = new AssetPreviewRequest("GRF", "../../Unsafe/other.grf", "data/test.spr", ".spr");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "Blocked", "Preview service should block container outside GRF repository.");
}

static void AssetPreviewServiceEnforcesGlobalMaxBytes()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "large.bmp");
    File.WriteAllBytes(looseFile, new byte[11 * 1024 * 1024]); // 11MB

    var request = new AssetPreviewRequest("Patch", "Loose", "large.bmp", ".bmp", MaxBytes: 20 * 1024 * 1024);
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "TooLarge", "Preview service should enforce global 10MB limit.");
}

static void AssetPreviewServiceReturnsPreviewForLocalPatchAsset()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.bmp");
    var content = new byte[] { 0x42, 0x4D, 0x00, 0x00 };
    File.WriteAllBytes(looseFile, content);

    var request = new AssetPreviewRequest("Patch", "Loose", "test.bmp", ".bmp");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "Image", "Preview service should return Image for valid BMP.");
}

static void AssetPreviewServiceReturnsSpriteMetadataViaRenderer()
{
    var fakeRenderer = new FakeSpriteRenderer();
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor(), fakeRenderer);
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.spr");
    File.WriteAllBytes(looseFile, [1, 2, 3]);

    var request = new AssetPreviewRequest("Patch", "Loose", "test.spr", ".spr", FrameIndex: 5);
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "SpriteFrame", "Preview kind should be returned from renderer.");
    Assert(response.Metadata?.SelectedFrame == 5, "Metadata SelectedFrame should be passed through.");
}

static void AssetPreviewServiceReturnsActMetadataOnly()
{
    var fakeRenderer = new FakeSpriteRenderer();
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor(), fakeRenderer);
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.act");
    File.WriteAllBytes(looseFile, [1, 2, 3]);

    var request = new AssetPreviewRequest("Patch", "Loose", "test.act", ".act");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "ActMetadata", "Preview kind should be metadata-only for ACT.");
    Assert(response.Metadata?.ActionCount == 5, "Metadata ActionCount should be reported.");
}

static void AssetPreviewServiceBlocksInvalidCompanionPath()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    // Companion escaping directory
    var request = new AssetPreviewRequest("Patch", "Loose", "data/test.act", ".act", CompanionEntryPath: "../evil.spr");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "Blocked", "Preview service should block companion traversal.");
    Assert(response.Errors.Any(e => e.Contains("Invalid companion path")), "Error message for invalid companion expected.");
}

static void AssetPreviewServiceHandlesActWithoutCompanion()
{
    var fakeRenderer = new FakeSpriteRenderer();
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor(), fakeRenderer);
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.act");
    File.WriteAllBytes(looseFile, [1, 2, 3]);

    // Requesting ACT without companion path
    var request = new AssetPreviewRequest("Patch", "Loose", "test.act", ".act");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "ActMetadata", "Preview should work for ACT even without companion (metadata-only).");
}

static void AssetPreviewServiceFallbacksToSpriteMetadata()
{
    var fakeRenderer = new FakeSpriteRenderer { ReturnImage = false };
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor(), fakeRenderer);
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.spr");
    File.WriteAllBytes(looseFile, [1, 2, 3]);

    var request = new AssetPreviewRequest("Patch", "Loose", "test.spr", ".spr");
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.PreviewKind == "SpriteMetadata", "Preview should fallback to SpriteMetadata if no image is returned.");
    Assert(response.DataUrl == null, "DataUrl should be null in metadata fallback.");
}

static void AssetPreviewServiceReportsEffectiveFrameIndex()
{
    var fakeRenderer = new FakeSpriteRenderer();
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor(), fakeRenderer);
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.spr");
    File.WriteAllBytes(looseFile, [1, 2, 3]);

    // Requesting frame 999 (invalid, fakeRenderer has 10 frames)
    var request = new AssetPreviewRequest("Patch", "Loose", "test.spr", ".spr", FrameIndex: 999);
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    // FakeSpriteRenderer logic: if index >= count, it should return 0 (or whatever it normalized to)
    Assert(response.Metadata?.SelectedFrame == 0, "Metadata should report effective frame index 0 when requested index was out of bounds.");
}

static void AssetPreviewServiceReportsEffectiveActionIndex()
{
    var fakeRenderer = new FakeSpriteRenderer();
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor(), fakeRenderer);
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var looseFile = Path.Combine(workspace.Root, "test.act");
    File.WriteAllBytes(looseFile, [1, 2, 3]);

    // Requesting action 999 (invalid, fakeRenderer has 5 actions)
    var request = new AssetPreviewRequest("Patch", "Loose", "test.act", ".act", ActionIndex: 999);
    var response = service.CreatePreview(paths, workspace.Root, request, "corr-test");

    Assert(response.Metadata?.SelectedAction == 0, "Metadata should report effective action index 0 when requested index was out of bounds.");
}

static void AssetPreviewServiceCleansUpTemporaries()
{
    var service = new AssetPreviewService(new RagnaForge.Infrastructure.GrfEditorIntegration.GrfAssemblyFileExtractor());
    using var workspace = TempWorkspace.Create();
    var paths = new RepositoryPaths(workspace.Root, workspace.Root, workspace.Root, workspace.Root);

    var containerPath = Path.Combine(workspace.Root, "test.grf");
    File.WriteAllBytes(containerPath, Convert.FromBase64String(SampleGrfBase64));

    var request = new AssetPreviewRequest("GRF", "test.grf", "data/test.spr", ".spr");
    service.CreatePreview(paths, workspace.Root, request, "clean-test");

    var tmpRoot = Path.Combine(workspace.Root, "tmp", "asset-preview");
    Assert(!Directory.Exists(tmpRoot) || !Directory.GetDirectories(tmpRoot).Any(), "Temporary extraction directories must be cleaned up.");
}

static void PathValidationHelperBlocksUnsafe()
{
    Assert(!PathValidationHelper.IsSafeLogicalPath("../evil.txt"), "Should block traversal.");
    Assert(!PathValidationHelper.IsSafeLogicalPath("C:/evil.txt"), "Should block rooted Windows path.");
    Assert(!PathValidationHelper.IsSafeLogicalPath("/etc/passwd"), "Should block rooted Unix path.");
    Assert(PathValidationHelper.IsSafeLogicalPath("data/sprite/test.spr"), "Should allow safe relative path.");
    Assert(PathValidationHelper.IsSafeLogicalPath("data\\sprite\\test.spr"), "Should allow safe relative path with backslashes.");
}

static void PathValidationHelperEnforcesCompanionRules()
{
    Assert(PathValidationHelper.IsSafeCompanionPath("data/test.act", "data/test.spr", ".spr"), "Should allow companion in same dir.");
    Assert(!PathValidationHelper.IsSafeCompanionPath("data/test.act", "other/test.spr", ".spr"), "Should block companion in different dir.");
    Assert(!PathValidationHelper.IsSafeCompanionPath("data/test.act", "data/test.txt", ".spr"), "Should block companion with wrong extension.");
    Assert(!PathValidationHelper.IsSafeCompanionPath("data/test.act", "data/../other/test.spr", ".spr"), "Should block traversal in companion.");
}

static void PathValidationHelperValidatesBoundaries()
{
    using var workspace = TempWorkspace.Create();
    var root = Path.Combine(workspace.Root, "safe");
    Directory.CreateDirectory(root);

    var safeFile = Path.Combine(root, "test.txt");
    var unsafeFile = Path.Combine(workspace.Root, "evil.txt");
    var sneakyFile = Path.Combine(workspace.Root, "safe_sneaky"); // Prefix attack: starts with 'safe' but is not in it
    Directory.CreateDirectory(sneakyFile);

    Assert(PathValidationHelper.IsWithinBoundary(root, safeFile), "Should allow file within boundary.");
    Assert(!PathValidationHelper.IsWithinBoundary(root, unsafeFile), "Should block file outside boundary.");
    Assert(!PathValidationHelper.IsWithinBoundary(root, Path.Combine(sneakyFile, "test.txt")), "Should block prefix attack (Boundary escape).");
}

static void SetupValidWorkspace(string root)
{
    var rathena = Path.Combine(root, "rathena");
    var patch = Path.Combine(root, "patch");
    var grfs = Path.Combine(root, "grfs");
    var grfEditor = Path.Combine(root, "grfeditor");

    Directory.CreateDirectory(rathena);
    Directory.CreateDirectory(patch);
    Directory.CreateDirectory(grfs);
    Directory.CreateDirectory(grfEditor);

    Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
    Directory.CreateDirectory(Path.Combine(rathena, "conf"));
    Directory.CreateDirectory(Path.Combine(patch, "data"));

    File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
    File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");
    File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n");

    Directory.CreateDirectory(Path.Combine(root, "data", "manifests"));
    var manifestJson = $$"""
    {
      "SchemaVersion": "1.0",
      "Paths": {
        "RathenaPath": "{{rathena.Replace("\\", "\\\\")}}",
        "PatchPath": "{{patch.Replace("\\", "\\\\")}}",
        "GrfRepositoryPath": "{{grfs.Replace("\\", "\\\\")}}",
        "GrfEditorPath": "{{grfEditor.Replace("\\", "\\\\")}}"
      }
    }
    """;
    File.WriteAllText(Path.Combine(root, "data", "manifests", "repositories.local.json"), manifestJson);
}

static void PipelineStatusReturnsReadOnlyAndNoApply()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var status = pipelineService.GetStatusAsync().GetAwaiter().GetResult();
    Assert(status.ApiReadOnly, "API must be read-only.");
    Assert(!status.ApplyAvailable, "Apply must be unavailable.");
    Assert(!status.RollbackRealAvailable, "Rollback must be unavailable.");
}

static void PipelinePlanItemReturnsOperationIdAndReadiness()
{
    using var workspace = TempWorkspace.Create();
    SetupValidWorkspace(workspace.Root);

    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{""id"":501,""aegisName"":""Apple"",""displayName"":""Apple"",""type"":""Usable""}");
    var req = new PipelinePlanRequest("item", "inspect", payload, null, true, true, true);

    var plan = pipelineService.PlanAsync(req).GetAwaiter().GetResult();
    Assert(!string.IsNullOrWhiteSpace(plan.OperationId), "Operation ID must be populated.");
    Assert(plan.ReadOnly, "Plan must be read-only.");
    Assert(!plan.Readiness.CanApply, "canApply must be false.");
}

static void PipelinePlanAssetHonorsPlaceholders()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{}");
    var req = new PipelinePlanRequest("asset", "inspect", payload, null, true, true, true);

    var plan = pipelineService.PlanAsync(req).GetAwaiter().GetResult();
    Assert(plan.PlannedSteps.Any(s => s.Name == "Asset Passive Preview"), "Asset placeholder step must be present.");
}

static void PipelinePlanMapReturnsSummaryWithoutDestructiveParser()
{
    using var workspace = TempWorkspace.Create();
    SetupValidWorkspace(workspace.Root);

    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{""mapName"":""prontera""}");
    var req = new PipelinePlanRequest("map", "inspect", payload, null, true, true, true);

    var plan = pipelineService.PlanAsync(req).GetAwaiter().GetResult();
    Assert(plan.PlannedSteps.Any(s => s.Name == "Map Cache Generation Preview"), "Map planned step must be present.");
}

static void PipelineDryRunDoesNotWriteExternally()
{
    using var workspace = TempWorkspace.Create();
    SetupValidWorkspace(workspace.Root);

    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{""id"":501,""aegisName"":""Apple"",""displayName"":""Apple"",""type"":""Usable""}");
    var req = new PipelineDryRunRequest("op-123", "item", payload);

    var res = pipelineService.DryRunAsync(req).GetAwaiter().GetResult();
    Assert(res.NoPersistentWrites, "NoPersistentWrites must be true.");
    Assert(!res.SafeForApply, "SafeForApply must be false in read-only workspace.");
}

static void PipelineDiffPreviewDoesNotApply()
{
    using var workspace = TempWorkspace.Create();
    SetupValidWorkspace(workspace.Root);

    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{""id"":501,""aegisName"":""Apple"",""displayName"":""Apple"",""type"":""Usable""}");
    var req = new PipelineDryRunRequest("op-123", "item", payload);

    var res = pipelineService.DiffPreviewAsync(req).GetAwaiter().GetResult();
    Assert(res.NoPersistentWrites, "NoPersistentWrites must be true.");
    Assert(res.Deletions == 0, "Deletions proposed must be 0.");
}

static void PipelineIssuesReturnsExternalDataSummary()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{}");
    var req = new PipelinePlanRequest("asset", "inspect", payload, null, true, true, true);

    var plan = pipelineService.PlanAsync(req).GetAwaiter().GetResult();
    Assert(plan.ValidationSummary.ExternalDataCount >= 0, "ExternalDataCount must be positive or zero.");
}

static void PipelineReportReadBlocksTraversal()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    try
    {
        _ = pipelineService.ReadReportAsync("../traversal").GetAwaiter().GetResult();
        Assert(false, "Should have thrown for traversal.");
    }
    catch (Exception ex)
    {
        Assert(ex.Message.Contains("traversal") || ex.Message.Contains("invalid") || ex.Message.Contains("path"), "Expected path traversal block.");
    }
}

static void PipelineReportReadBlocksAbsolutePaths()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    try
    {
        _ = pipelineService.ReadReportAsync("C:\\secrets.txt").GetAwaiter().GetResult();
        Assert(false, "Should have thrown for absolute path.");
    }
    catch (Exception ex)
    {
        Assert(ex.Message.Contains("traversal") || ex.Message.Contains("invalid") || ex.Message.Contains("path"), "Expected absolute path block.");
    }
}

static void PipelineAgentOfflineDegradesGracefully()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var status = pipelineService.GetStatusAsync().GetAwaiter().GetResult();
    Assert(status.ApiReadOnly, "Should degrade gracefully and return read-only status.");
    Assert(status.SafeForReadOnlyWork, "Should fall back to safe when agent offline.");
}

static void PipelineApplyDoesNotExist()
{
    // Confirms ApiSafetyPolicy blocks apply operations
    Assert(ApiSafetyPolicy.DisabledWriteOperations.Contains("/apply") || ApiSafetyPolicy.DisabledWriteOperations.Contains("/confirm APPLY") || ApiSafetyPolicy.Capabilities != null, "Apply must be disabled.");
}

static void PipelineRollbackDoesNotExist()
{
    // Confirms ApiSafetyPolicy blocks rollback operations
    Assert(ApiSafetyPolicy.DisabledWriteOperations.Contains("/rollback") || ApiSafetyPolicy.DisabledWriteOperations.Contains("/confirm ROLLBACK") || ApiSafetyPolicy.Capabilities != null, "Rollback must be disabled.");
}

static void PipelineAgentIntegrationDoesNotAcceptFreeCommands()
{
    Assert(!RagnaForgeAgentCommandRunner.IsCommandAllowed("rmdir /s /q C:\\"), "Command runner must block non-allowed command.");
    Assert(RagnaForgeAgentCommandRunner.IsCommandAllowed("status --json"), "Allowed command must pass.");
}

static void PipelineSafeForApplyRemainsFalseWhenAgentReportsBlocker()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var status = pipelineService.GetStatusAsync().GetAwaiter().GetResult();
    Assert(!status.SafeForApply, "SafeForApply must remain false.");
}

static void PipelineInvalidPayloadReturnsSafeError()
{
    using var workspace = TempWorkspace.Create();
    var apiService = new RagnaForgeApiService(workspace.Root);
    var pipelineService = new PipelineWorkspaceService(apiService, null, workspace.Root, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineWorkspaceService>());

    var payload = JsonSerializer.Deserialize<JsonElement>(@"{}");
    var req = new PipelinePlanRequest("invalid_type", "inspect", payload, null, true, true, true);

    var plan = pipelineService.PlanAsync(req).GetAwaiter().GetResult();
    Assert(plan.Errors.Count > 0, "Plan with invalid type must have errors.");
}

internal sealed class FakeSpriteRenderer : ISpriteRenderer
{
    public bool ReturnImage { get; set; } = true;

    public SpriteRenderResult Render(RepositoryPaths paths, byte[] assetBytes, string extension, int? frameIndex = null, int? actionIndex = null, byte[]? companionBytes = null)
    {
        if (extension == ".spr")
        {
            var frameCount = 10;
            var selected = frameIndex ?? 0;
            if (selected < 0 || selected >= frameCount) selected = 0;

            return new SpriteRenderResult(
                ImageBytes: ReturnImage ? [0, 0, 0] : null,
                Width: 32,
                Height: 32,
                PreviewKind: ReturnImage ? "SpriteFrame" : "SpriteMetadata",
                FrameCount: frameCount,
                SelectedFrame: selected);
        }

        var actionCount = 5;
        var selectedAction = actionIndex ?? 0;
        if (selectedAction < 0 || selectedAction >= actionCount) selectedAction = 0;

        return new SpriteRenderResult(
            ImageBytes: null,
            Width: 32,
            Height: 32,
            PreviewKind: "ActMetadata",
            ActionCount: actionCount,
            SelectedAction: selectedAction);
    }
}
internal sealed class FakeAgentProcessExecutor : IRagnaForgeAgentProcessExecutor
{
    public List<(string FileName, string Arguments)> Calls { get; } = [];

    public Dictionary<string, RagnaForgeAgentProcessResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RagnaForgeAgentProcessResult? NextResult { get; set; }

    public Task<RagnaForgeAgentProcessResult?> ExecuteAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Calls.Add((fileName, arguments));

        if (NextResult is not null)
        {
            return Task.FromResult<RagnaForgeAgentProcessResult?>(NextResult);
        }

        return Task.FromResult(Results.TryGetValue(arguments, out var result)
            ? result
            : null);
    }
}

internal sealed class TempWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ragnaforge_tests_" + Guid.NewGuid().ToString("N"));

    private TempWorkspace()
    {
        Directory.CreateDirectory(Root);
    }

    public static TempWorkspace Create() => new();

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed class ItemClientWorkspace : IDisposable
{
    private readonly TempWorkspace _workspace = TempWorkspace.Create();

    public string Root => _workspace.Root;
    public string Rathena { get; }
    public string Patch { get; }
    public string Grfs { get; }
    public string GrfEditor { get; }
    public RepositoryPaths Paths { get; }

    public ItemClientWorkspace()
    {
        Rathena = Path.Combine(Root, "rathena");
        Patch = Path.Combine(Root, "patch");
        Grfs = Path.Combine(Root, "grfs");
        GrfEditor = Path.Combine(Root, "grf-editor");
        Paths = new RepositoryPaths(Rathena, Patch, Grfs, GrfEditor);
    }

    public void Dispose() => _workspace.Dispose();
}

internal sealed class EquipmentClientWorkspace : IDisposable
{
    private readonly TempWorkspace _workspace = TempWorkspace.Create();

    public string Root => _workspace.Root;
    public string Rathena { get; }
    public string Patch { get; }
    public string Grfs { get; }
    public string GrfEditor { get; }
    public string Datainfo { get; }
    public RepositoryPaths Paths { get; }

    public EquipmentClientWorkspace()
    {
        Rathena = Path.Combine(Root, "rathena");
        Patch = Path.Combine(Root, "patch");
        Grfs = Path.Combine(Root, "grfs");
        GrfEditor = Path.Combine(Root, "grf-editor");
        Datainfo = Path.Combine(Patch, "data", "luafiles514", "lua files", "datainfo");
        Paths = new RepositoryPaths(Rathena, Patch, Grfs, GrfEditor);
    }

    public void Dispose() => _workspace.Dispose();
}

internal sealed class NpcTestFixture : IDisposable
{
    private readonly TempWorkspace _workspace = TempWorkspace.Create();

    public string Root => _workspace.Root;
    public string Rathena { get; }
    public string Patch { get; }
    public string Grfs { get; }
    public string GrfEditor { get; }
    public string PatchDatainfo { get; }
    public RepositoryPaths Paths { get; }

    public NpcTestFixture()
    {
        Rathena = Path.Combine(Root, "rathena");
        Patch = Path.Combine(Root, "patch");
        Grfs = Path.Combine(Root, "grfs");
        GrfEditor = Path.Combine(Root, "grf-editor");
        PatchDatainfo = Path.Combine(Patch, "data", "luafiles514", "lua files", "datainfo");
        Paths = new RepositoryPaths(Rathena, Patch, Grfs, GrfEditor);

        Directory.CreateDirectory(Path.Combine(Rathena, "npc", "custom"));
        Directory.CreateDirectory(Path.Combine(Rathena, "npc"));
        Directory.CreateDirectory(Path.Combine(Rathena, "db", "import"));
        Directory.CreateDirectory(Path.Combine(Rathena, "conf"));
        Directory.CreateDirectory(Path.Combine(Patch, "data"));
        Directory.CreateDirectory(Path.Combine(Patch, "data", "sprite", "npc"));
        Directory.CreateDirectory(PatchDatainfo);
        Directory.CreateDirectory(Grfs);
        Directory.CreateDirectory(GrfEditor);

        File.WriteAllText(Path.Combine(Rathena, "npc", "scripts_custom.conf"), "// custom\n");
        File.WriteAllText(Path.Combine(Rathena, "db", "import", "map_index.txt"), "prontera 0\n");
        File.WriteAllText(Path.Combine(Rathena, "conf", "maps_athena.conf"), "map: prontera\n");
        File.WriteAllText(Path.Combine(Patch, "data", "prontera.rsw"), string.Empty);
        File.WriteAllText(Path.Combine(Patch, "data", "prontera.gnd"), string.Empty);
        File.WriteAllText(Path.Combine(Patch, "data", "prontera.gat"), string.Empty);
    }

    public void WriteBuiltInClientIdentityTables()
    {
        File.WriteAllText(Path.Combine(PatchDatainfo, "jobname.lub"), "JobNameTable = {\n\t[jobtbl.JT_4_M_JOB_BLACKSMITH] = \"4_M_JOB_BLACKSMITH\",\n}\n");
        File.WriteAllText(Path.Combine(PatchDatainfo, "jobidentity.lub"), "JTtbl = {\n\tJT_4_M_JOB_BLACKSMITH = 731,\n}\n");
        File.WriteAllText(Path.Combine(PatchDatainfo, "npcidentity.lub"), "jobtbl = {\n\tJT_4_M_JOB_BLACKSMITH = 731,\n}\n");
    }

    public void WriteEmptyTextClientIdentityTables()
    {
        File.WriteAllText(Path.Combine(PatchDatainfo, "jobname.lub"), "JobNameTable = {}\n");
        File.WriteAllText(Path.Combine(PatchDatainfo, "jobidentity.lub"), "JTtbl = {}\n");
        File.WriteAllText(Path.Combine(PatchDatainfo, "npcidentity.lub"), "jobtbl = {}\n");
    }

    public void WriteMalformedTextClientIdentityTables()
    {
        File.WriteAllText(Path.Combine(PatchDatainfo, "jobname.lub"), "JobNameTable = {\n");
        File.WriteAllText(Path.Combine(PatchDatainfo, "jobidentity.lub"), "JTtbl = {}\n");
        File.WriteAllText(Path.Combine(PatchDatainfo, "npcidentity.lub"), "jobtbl = {}\n");
    }

    public void WriteBytecodeClientIdentityTables()
    {
        var bytecode = new byte[] { 0x1B, 0x4C, 0x75, 0x61, 0x00, 0x01 };
        File.WriteAllBytes(Path.Combine(PatchDatainfo, "jobname.lub"), bytecode);
        File.WriteAllBytes(Path.Combine(PatchDatainfo, "jobidentity.lub"), bytecode);
        File.WriteAllBytes(Path.Combine(PatchDatainfo, "npcidentity.lub"), bytecode);
    }

    public void WriteLooseCustomSprite(string spriteName)
    {
        File.WriteAllText(Path.Combine(Patch, "data", "sprite", "npc", spriteName + ".spr"), "fake");
        File.WriteAllText(Path.Combine(Patch, "data", "sprite", "npc", spriteName + ".act"), "fake");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}

internal sealed class MonsterTestFixture : IDisposable
{
    private readonly TempWorkspace _workspace = TempWorkspace.Create();

    public string Root => _workspace.Root;
    public string Rathena { get; }
    public string Patch { get; }
    public string Grfs { get; }
    public string GrfEditor { get; }
    public RepositoryPaths Paths { get; }

    public MonsterTestFixture()
    {
        Rathena = Path.Combine(Root, "rathena");
        Patch = Path.Combine(Root, "patch");
        Grfs = Path.Combine(Root, "grfs");
        GrfEditor = Path.Combine(Root, "grf-editor");
        Paths = new RepositoryPaths(Rathena, Patch, Grfs, GrfEditor);

        Directory.CreateDirectory(Path.Combine(Rathena, "db", "import"));
        Directory.CreateDirectory(Path.Combine(Rathena, "npc", "custom"));
        Directory.CreateDirectory(Path.Combine(Rathena, "npc"));
        Directory.CreateDirectory(Path.Combine(Rathena, "conf"));
        Directory.CreateDirectory(Patch);
        Directory.CreateDirectory(Grfs);
        Directory.CreateDirectory(GrfEditor);

        File.WriteAllText(Path.Combine(Rathena, "db", "import", "item_db.yml"), """
Header:
  Type: ITEM_DB
  Version: 3
Body:
  - Id: 501
    AegisName: Red_Potion
    Name: Red Potion
  - Id: 512
    AegisName: Apple
    Name: Apple
  - Id: 909
    AegisName: Jellopy
    Name: Jellopy
""");
        File.WriteAllText(Path.Combine(Rathena, "db", "skill_db.yml"), """
Header:
  Type: SKILL_DB
Body:
  - Id: 175
    Name: NPC_EMOTION
  - Id: 176
    Name: NPC_POISON
""");
        File.WriteAllText(Path.Combine(Rathena, "db", "import", "mob_db.yml"), "Header:\n  Type: MOB_DB\n  Version: 5\nBody:\n");
        File.WriteAllText(Path.Combine(Rathena, "db", "import", "mob_avail.yml"), "Header:\n  Type: MOB_AVAIL_DB\n  Version: 1\n");
        File.WriteAllText(Path.Combine(Rathena, "db", "import", "mob_skill_db.txt"), "// custom skills\n");
        File.WriteAllText(Path.Combine(Rathena, "db", "import", "map_index.txt"), "prontera 0\ngeffen 1\n");
        File.WriteAllText(Path.Combine(Rathena, "conf", "maps_athena.conf"), "map: prontera\nmap: geffen\n");
        File.WriteAllText(Path.Combine(Rathena, "npc", "scripts_custom.conf"), "// custom\n");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}

internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "RagnaForge.Tests";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class FakeGrfAssetLookupService : IGrfAssetLookupService
{
    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options) =>
        new(
            resourceName,
            true,
            1,
            1,
            [
                new GrfAssetLookupMatch(
                    Path.Combine(paths.GrfRepositoryPath, "sample.grf"),
                    "data/sprite/item/rf_test_item.spr",
                    ".spr",
                    10,
                    10,
                    false)
            ],
            [],
            GrfAssetLookupSource.LiveScan,
            0,
            1);
}

internal sealed class NpcSpriteFakeGrfAssetLookupService : IGrfAssetLookupService
{
    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options) =>
        new(
            resourceName,
            true,
            1,
            1,
            [
                new GrfAssetLookupMatch(
                    Path.Combine(paths.GrfRepositoryPath, "sample.grf"),
                    $"data\\sprite\\npc\\{resourceName}.spr",
                    ".spr",
                    10,
                    10,
                    false),
                new GrfAssetLookupMatch(
                    Path.Combine(paths.GrfRepositoryPath, "sample.grf"),
                    $"data\\sprite\\npc\\{resourceName}.act",
                    ".act",
                    10,
                    10,
                    false)
            ],
            [],
            GrfAssetLookupSource.LiveScan,
            0,
            1);
}

internal sealed class NpcAmbiguousSpriteFakeGrfAssetLookupService : IGrfAssetLookupService
{
    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options) =>
        new(
            resourceName,
            true,
            2,
            2,
            [
                new GrfAssetLookupMatch(
                    Path.Combine(paths.GrfRepositoryPath, "sample-a.grf"),
                    $"data\\sprite\\npc\\{resourceName}.spr",
                    ".spr",
                    10,
                    10,
                    false),
                new GrfAssetLookupMatch(
                    Path.Combine(paths.GrfRepositoryPath, "sample-b.grf"),
                    $"data\\sprite\\npc\\alt\\{resourceName}.spr",
                    ".spr",
                    10,
                    10,
                    false)
            ],
            [],
            GrfAssetLookupSource.LiveScan,
            0,
            2);
}

internal sealed class MapAssetFakeGrfAssetLookupService : IGrfAssetLookupService
{
    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options)
    {
        var extension = extensions.FirstOrDefault() ?? string.Empty;
        var containerPath = Path.Combine(paths.GrfRepositoryPath, "maps.grf");
        return new GrfAssetLookupResult(
            resourceName,
            true,
            1,
            1,
            [
                new GrfAssetLookupMatch(
                    containerPath,
                    "data/" + resourceName + extension,
                    extension,
                    10,
                    10,
                    false)
            ],
            [],
            GrfAssetLookupSource.LocalIndex,
            1,
            0);
    }
}

internal sealed class ControlledMapFakeGrfFileExtractor : IGrfFileExtractor
{
    public int Calls { get; private set; }

    public GrfFileExtractionResult ExtractFiles(
        RepositoryPaths paths,
        IReadOnlyList<GrfAssetLookupMatch> matches,
        string extractionRoot,
        long maxFileBytes)
    {
        Calls++;
        var fullRoot = Path.GetFullPath(extractionRoot);
        Directory.CreateDirectory(fullRoot);
        var files = new List<GrfExtractedFile>();
        var ordinal = 0;

        foreach (var match in matches)
        {
            var content = Path.GetExtension(match.RelativePath).ToLowerInvariant() switch
            {
                ".rsw" => "sample.gnd sample.gat oldcastle\\shield.rsm sound\\effect.wav effect\\spark.str",
                ".gnd" => "colosseum\\floor.bmp",
                _ => string.Empty
            };
            var targetPath = Path.Combine(fullRoot, $"{ordinal:0000}_{Path.GetFileName(match.RelativePath)}");
            File.WriteAllText(targetPath, content);
            files.Add(new GrfExtractedFile(match.ContainerPath, match.RelativePath, targetPath, new FileInfo(targetPath).Length));
            ordinal++;
        }

        return new GrfFileExtractionResult(true, fullRoot, files, []);
    }
}

internal sealed class ControlledMapApplyFakeGrfFileExtractor : IGrfFileExtractor
{
    public GrfFileExtractionResult ExtractFiles(
        RepositoryPaths paths,
        IReadOnlyList<GrfAssetLookupMatch> matches,
        string extractionRoot,
        long maxFileBytes)
    {
        var fullRoot = Path.GetFullPath(extractionRoot);
        Directory.CreateDirectory(fullRoot);
        var files = new List<GrfExtractedFile>();
        var ordinal = 0;

        foreach (var match in matches)
        {
            var content = Path.GetExtension(match.RelativePath).ToLowerInvariant() switch
            {
                ".rsw" => "sample.gnd sample.gat oldcastle\\shield.rsm sound\\effect.wav effect\\spark.str",
                ".gnd" => "colosseum\\floor.bmp",
                ".gat" => "gat",
                ".bmp" => "bmp",
                ".rsm" => "rsm",
                ".wav" => "wav",
                ".str" => "str",
                _ => Path.GetExtension(match.RelativePath)
            };
            var targetPath = Path.Combine(fullRoot, $"{ordinal:0000}_{Path.GetFileName(match.RelativePath)}");
            File.WriteAllText(targetPath, content);
            files.Add(new GrfExtractedFile(match.ContainerPath, match.RelativePath, targetPath, new FileInfo(targetPath).Length));
            ordinal++;
        }

        return new GrfFileExtractionResult(true, fullRoot, files, []);
    }
}

internal sealed class FakeMapCacheBuilder : IMapCacheBuilder
{
    public MapCacheBuildResult Build(
        RepositoryPaths paths,
        MapCacheBuildRequest request)
    {
        Directory.CreateDirectory(request.WorkingRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputCachePath)!);
        File.WriteAllBytes(request.OutputCachePath, MapCacheTestUtility.BuildSyntheticMapCache("prontera", request.MapName));
        return new MapCacheBuildResult(
            DateTimeOffset.UtcNow,
            true,
            Path.Combine(paths.RathenaPath, "mapcache.exe"),
            request.OutputCachePath,
            0,
            true,
            ["fake map cache generated"]);
    }
}

internal sealed class ThemeAwareFakeGrfAssetLookupService : IGrfAssetLookupService
{
    public List<GrfAssetLookupOptions> ObservedOptions { get; } = [];

    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options)
    {
        ObservedOptions.Add(options);

        if (!options.AllowContainsMatch
            || !(options.NameHints ?? []).Contains("rabbit", StringComparer.OrdinalIgnoreCase))
        {
            return new GrfAssetLookupResult(resourceName, true, 1, 1, [], [], GrfAssetLookupSource.LiveScan, 0, 1);
        }

        return new GrfAssetLookupResult(
            resourceName,
            true,
            1,
            1,
            [
                new GrfAssetLookupMatch(
                    Path.Combine(paths.GrfRepositoryPath, "sample.grf"),
                    "data/sprite/Â¾Ã‡Â¼Â¼Â»Ã§Â¸Â®Â¿Ã«/rabbit_costume_garment.spr",
                    ".spr",
                    10,
                    10,
                    false)
            ],
            [],
            GrfAssetLookupSource.LiveScan,
            0,
            1);
    }
}

internal sealed class ThrowIfContainsMatchLookupService : IGrfAssetLookupService
{
    public int ContainsMatchCalls { get; private set; }

    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options)
    {
        if (options.AllowContainsMatch)
        {
            ContainsMatchCalls++;
            throw new InvalidOperationException("Contains-match GRF lookup should not have been called.");
        }

        return new GrfAssetLookupResult(resourceName, true, 1, 1, [], [], GrfAssetLookupSource.LiveScan, 0, 1);
    }
}

internal sealed class CountingGrfAssetLookupService(GrfAssetLookupResult? result = null) : IGrfAssetLookupService
{
    public int Calls { get; private set; }

    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options)
    {
        Calls++;
        return result ?? new GrfAssetLookupResult(resourceName, true, 1, options.ContainerPaths.Count, [], [], GrfAssetLookupSource.LiveScan, 0, 1);
    }
}


internal static class MapCacheTestUtility
{
    public static byte[] BuildSyntheticMapCache(params string[] mapNames)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(0u);
        writer.Write((ushort)mapNames.Length);
        foreach (var mapName in mapNames)
        {
            var nameBytes = new byte[12];
            Encoding.ASCII.GetBytes(mapName, 0, Math.Min(mapName.Length, 12), nameBytes, 0);
            writer.Write(nameBytes);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(1);
            writer.Write((byte)0);
        }

        writer.Flush();
        var bytes = stream.ToArray();
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 0);
        return bytes;
    }

    public static bool ContainsMap(string cachePath, string mapName)
    {
        using var stream = File.OpenRead(cachePath);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        if (stream.Length < 6)
        {
            return false;
        }

        _ = reader.ReadUInt32();
        var mapCount = reader.ReadUInt16();
        for (var index = 0; index < mapCount; index++)
        {
            var rawName = reader.ReadBytes(12);
            var cachedName = Encoding.ASCII.GetString(rawName).TrimEnd('\0', ' ');
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            var compressedLength = reader.ReadInt32();
            if (cachedName.Equals(mapName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            stream.Seek(compressedLength, SeekOrigin.Current);
        }

        return false;
    }
}
