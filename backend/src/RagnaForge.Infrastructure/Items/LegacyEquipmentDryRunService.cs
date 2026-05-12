using System.Text;
using System.Text.RegularExpressions;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Discovery;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Visuals;
using RagnaForge.Infrastructure.Grf;
using RagnaForge.Infrastructure.FileSystem;

namespace RagnaForge.Infrastructure.Items;

public sealed class LegacyEquipmentDryRunService
{
    private static readonly string[] VisualAssetExtensions = [".spr", ".act"];
    private static readonly HashSet<string> KnownEquipLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Head_Top",
        "Head_Mid",
        "Head_Low",
        "Armor",
        "Right_Hand",
        "Left_Hand",
        "Garment",
        "Shoes",
        "Right_Accessory",
        "Left_Accessory",
        "Costume_Head_Top",
        "Costume_Head_Mid",
        "Costume_Head_Low",
        "Costume_Garment",
        "Ammo",
        "Shadow_Armor",
        "Shadow_Weapon",
        "Shadow_Shield",
        "Shadow_Shoes",
        "Shadow_Right_Accessory",
        "Shadow_Left_Accessory",
        "Both_Hand",
        "Both_Accessory"
    };

    private static readonly HashSet<string> KnownWeaponBaseTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "WEAPONTYPE_SHORTSWORD",
        "WEAPONTYPE_SWORD",
        "WEAPONTYPE_TWOHANDSWORD",
        "WEAPONTYPE_SPEAR",
        "WEAPONTYPE_TWOHANDSPEAR",
        "WEAPONTYPE_AXE",
        "WEAPONTYPE_TWOHANDAXE",
        "WEAPONTYPE_MACE",
        "WEAPONTYPE_TWOHANDMACE",
        "WEAPONTYPE_ROD",
        "WEAPONTYPE_BOW",
        "WEAPONTYPE_KNUKLE",
        "WEAPONTYPE_INSTRUMENT",
        "WEAPONTYPE_WHIP",
        "WEAPONTYPE_BOOK",
        "WEAPONTYPE_CATARRH",
        "WPCLASS_GUN_HANDGUN",
        "WPCLASS_GUN_RIFLE",
        "WPCLASS_GUN_GATLING",
        "WPCLASS_GUN_SHOTGUN",
        "WPCLASS_GUN_GRANADE",
        "WPCLASS_SYURIKEN",
        "WPCLASS_TWOHANDROD"
    };

    private static readonly HashSet<int> KnownShieldViewIds = [1, 2, 3, 4, 5, 6];

    private readonly IGrfAssetLookupService? _grfAssetLookupService;
    private readonly IGrfContainerIndexStore? _grfContainerIndexStore;
    private readonly GrfAssetLookupOptions _grfAssetLookupOptions;

    public LegacyEquipmentDryRunService()
        : this(null, null, GrfAssetLookupOptions.Disabled)
    {
    }

    public LegacyEquipmentDryRunService(
        IGrfAssetLookupService? grfAssetLookupService,
        GrfAssetLookupOptions grfAssetLookupOptions)
        : this(grfAssetLookupService, new JsonGrfContainerIndexStore(Directory.GetCurrentDirectory()), grfAssetLookupOptions)
    {
    }

    public LegacyEquipmentDryRunService(
        IGrfAssetLookupService? grfAssetLookupService,
        IGrfContainerIndexStore? grfContainerIndexStore,
        GrfAssetLookupOptions grfAssetLookupOptions)
    {
        _grfAssetLookupService = grfAssetLookupService;
        _grfContainerIndexStore = grfContainerIndexStore;
        _grfAssetLookupOptions = grfAssetLookupOptions;
    }

    public EquipmentDryRunReport Create(
        RepositoryPaths paths,
        EpisodeProfile episodeProfile,
        EquipmentDefinitionInput input,
        VisualThemeEvaluation? visualTheme = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(episodeProfile);
        ArgumentNullException.ThrowIfNull(input);

        var itemService = _grfAssetLookupService is null
            ? new LegacyItemDryRunService()
            : new LegacyItemDryRunService(_grfAssetLookupService, _grfAssetLookupOptions);
        var itemReport = itemService.Create(paths, episodeProfile, input.Item);

        var warnings = itemReport.Warnings
            .Where(message => !message.Contains("item-first", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var dependencies = itemReport.Dependencies
            .Where(dependency => dependency.Category != "DryRun")
            .ToList();
        var proposedChanges = itemReport.ProposedChanges.ToList();
        var visualAssetLookup = ResolveVisualAssetLookup(paths, input);
        var clientSidePlanner = new ClientSidePlanner();

        dependencies.AddRange(BuildEquipmentDependencies(paths, input, visualTheme, visualAssetLookup));

        if (proposedChanges.Count > 0)
        {
            proposedChanges[0] = proposedChanges[0] with
            {
                Preview = BuildEquipmentItemDbSnippet(itemReport.ResolvedId, input)
            };
        }

        var visualChanges = BuildVisualChanges(paths, input).ToArray();
        var visualClientSidePlan = clientSidePlanner.CreateVisualPlan(paths.PatchPath, visualChanges);
        foreach (var block in visualClientSidePlan.BlockReasons)
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Missing, block));
        }

        foreach (var warning in visualClientSidePlan.ValidationWarnings)
        {
            dependencies.Add(new ItemDependency("Patch", ItemDependencyState.Warning, warning));
        }

        foreach (var change in visualClientSidePlan.CanApply ? visualClientSidePlan.ProposedChanges : [])
        {
            proposedChanges.Add(change);
        }

        dependencies.Add(new ItemDependency(
            "DryRun",
            ItemDependencyState.Proposed,
            $"Prepared {proposedChanges.Count} proposed file change(s) without touching external repositories."));

        var canApply = dependencies.All(item => item.State != ItemDependencyState.Missing);
        var diffPreview = ItemDiffPreviewBuilder.Build(proposedChanges);

        return new EquipmentDryRunReport(
            itemReport.GeneratedAtUtc,
            episodeProfile,
            itemReport.ClientDateUsed,
            itemReport.ClientDateSource,
            itemReport.ClientItemMode,
            input,
            itemReport.ResolvedId,
            canApply,
            dependencies,
            proposedChanges,
            diffPreview,
            warnings,
            visualTheme,
            itemReport.AssetLookup,
            visualAssetLookup,
            itemReport.ClientSidePlan,
            visualClientSidePlan);
    }

    private IEnumerable<ItemDependency> BuildEquipmentDependencies(
        RepositoryPaths paths,
        EquipmentDefinitionInput input,
        VisualThemeEvaluation? visualTheme,
        GrfAssetLookupResult? visualAssetLookup)
    {
        if (input.EquipLocations.Count == 0)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "At least one equip location is required for an equipment dry-run.");
        }
        else
        {
            var invalidLocations = input.EquipLocations
                .Where(location => !KnownEquipLocations.Contains(location))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (invalidLocations.Length > 0)
            {
                yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Unknown rAthena equip location(s): {string.Join(", ", invalidLocations)}.");
            }
            else
            {
                yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Equip locations resolved: {string.Join(", ", input.EquipLocations)}.");
            }
        }

        if (!LooksLikeEquipmentType(input.Item.Type))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Item type '{input.Item.Type}' does not look like an equipment type for this milestone.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Item type '{input.Item.Type}' is compatible with equipment dry-run.");
        }

        if (input.EquipLevelMin is not null)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Equip level minimum set to {input.EquipLevelMin}.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Warning, "Equip level minimum was not provided.");
        }

        if (input.AllowedJobs.Count > 0)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Job restriction list contains {input.AllowedJobs.Count} entries.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Warning, "Job restrictions were not provided; the equipment snippet will omit a Jobs block.");
        }

        var category = NormalizeVisualCategory(input.VisualCategory);
        if (category is null)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Warning, "No visual category was provided; no datainfo visual registration will be proposed.");
            yield break;
        }

        if (category == "unsupported")
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Visual category '{input.VisualCategory}' is not supported in this milestone.");
            yield break;
        }

        if (input.ViewId is null)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "View ID is required when a visual category is provided.");
        }
        else if (input.ViewId <= 0)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "View ID must be greater than zero for visual registration.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"View ID {input.ViewId} will be used for visual registration.");
        }

        if (category == "shield")
        {
            foreach (var dependency in BuildShieldDependencies(paths.PatchPath, input))
            {
                yield return dependency;
            }

            yield break;
        }

        if (string.IsNullOrWhiteSpace(input.ClientSymbolName))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Client symbol name is required for visual registration.");
        }
        else if (!IsSafeLuaIdentifier(input.ClientSymbolName))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Client symbol name '{input.ClientSymbolName}' is not a safe Lua identifier.");
        }
        else if (!ClientSymbolMatchesCategory(category, input.ClientSymbolName))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Client symbol name '{input.ClientSymbolName}' does not match the expected prefix for visual category '{category}'.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Client symbol name '{input.ClientSymbolName}' is set.");
        }

        if (string.IsNullOrWhiteSpace(input.ClientSpriteName))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Client sprite name is required for visual registration.");
        }
        else if (!IsSafeLuaStringLiteral(input.ClientSpriteName))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Client sprite name '{input.ClientSpriteName}' is not safe for direct Lua string emission.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Client sprite name '{input.ClientSpriteName}' is set.");
        }

        if (category == "weapon")
        {
            foreach (var dependency in BuildWeaponDependencies(input))
            {
                yield return dependency;
            }
        }

        foreach (var datainfoPath in ResolveVisualDatainfoTargets(paths.PatchPath, category))
        {
            if (SafeFileSystem.FileExists(datainfoPath))
            {
                yield return new ItemDependency("Patch", ItemDependencyState.Satisfied, $"Visual datainfo file is available.", datainfoPath);
                foreach (var duplicateDependency in BuildVisualDuplicateDependencies(datainfoPath, category, input))
                {
                    yield return duplicateDependency;
                }
            }
            else
            {
                yield return new ItemDependency("Patch", ItemDependencyState.Missing, $"Required visual datainfo file was not found.", datainfoPath);
            }
        }

        foreach (var dependency in BuildVisualAssetDependencies(paths, input, visualTheme, visualAssetLookup))
        {
            yield return dependency;
        }
    }

    private static IEnumerable<ProposedFileChange> BuildVisualChanges(
        RepositoryPaths paths,
        EquipmentDefinitionInput input)
    {
        var category = NormalizeVisualCategory(input.VisualCategory);
        if (category is null
            || category is "unsupported" or "shield"
            || input.ViewId is null or <= 0
            || string.IsNullOrWhiteSpace(input.ClientSymbolName)
            || string.IsNullOrWhiteSpace(input.ClientSpriteName)
            || !IsSafeLuaIdentifier(input.ClientSymbolName)
            || !IsSafeLuaStringLiteral(input.ClientSpriteName)
            || !ClientSymbolMatchesCategory(category, input.ClientSymbolName))
        {
            yield break;
        }

        var datainfoRoot = SafeFileSystem.Combine(paths.PatchPath, "data", "luafiles514", "lua files", "datainfo");
        if (category is "headgear" or "accessory")
        {
            var accessoryIdPath = SafeFileSystem.Combine(datainfoRoot, "accessoryid.lub");
            var accNamePath = SafeFileSystem.Combine(datainfoRoot, "accname.lub");

            yield return new ProposedFileChange(
                accessoryIdPath,
                "append",
                SafeFileSystem.FileExists(accessoryIdPath),
                $"\nACCESSORY_IDs.{input.ClientSymbolName} = {input.ViewId}");

            yield return new ProposedFileChange(
                accNamePath,
                "append",
                SafeFileSystem.FileExists(accNamePath),
                $"\nAccNameTable[ACCESSORY_IDs.{input.ClientSymbolName}] = \"{input.ClientSpriteName}\"");
        }
        else if (category == "robe")
        {
            var robeIdPath = SafeFileSystem.Combine(datainfoRoot, "spriterobeid.lub");
            var robeNamePath = SafeFileSystem.Combine(datainfoRoot, "spriterobename.lub");

            yield return new ProposedFileChange(
                robeIdPath,
                "append",
                SafeFileSystem.FileExists(robeIdPath),
                $"\nSPRITE_ROBE_IDs.{input.ClientSymbolName} = {input.ViewId}");

            yield return new ProposedFileChange(
                robeNamePath,
                "append",
                SafeFileSystem.FileExists(robeNamePath),
                BuildRobeNameAppendBlock(input.ClientSymbolName, input.ClientSpriteName));
        }
        else if (category == "weapon" && NormalizeWeaponBaseType(input.WeaponBaseType) is { } weaponBaseType)
        {
            var weaponTablePath = SafeFileSystem.Combine(datainfoRoot, "weapontable.lub");

            yield return new ProposedFileChange(
                weaponTablePath,
                "append",
                SafeFileSystem.FileExists(weaponTablePath),
                BuildWeaponAppendBlock(input, weaponBaseType));
        }
    }

    private static string BuildEquipmentItemDbSnippet(int resolvedId, EquipmentDefinitionInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("# RagnaForge equipment dry-run proposal");
        builder.AppendLine($"  - Id: {resolvedId}");
        builder.AppendLine($"    AegisName: {input.Item.AegisName}");
        builder.AppendLine($"    Name: {input.Item.DisplayName}");
        builder.AppendLine($"    Type: {input.Item.Type}");
        builder.AppendLine($"    Buy: {input.Item.Buy}");
        builder.AppendLine($"    Sell: {input.Item.Sell}");
        builder.AppendLine($"    Weight: {input.Item.Weight}");

        if (input.Defense is not null)
        {
            builder.AppendLine($"    Defense: {input.Defense}");
        }

        if (input.Item.Slots > 0)
        {
            builder.AppendLine($"    Slots: {input.Item.Slots}");
        }

        if (input.AllowedJobs.Count > 0)
        {
            builder.AppendLine("    Jobs:");
            foreach (var job in input.AllowedJobs)
            {
                builder.AppendLine($"      {job}: true");
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Gender))
        {
            builder.AppendLine($"    Gender: {input.Gender}");
        }

        builder.AppendLine("    Locations:");
        foreach (var location in input.EquipLocations)
        {
            builder.AppendLine($"      {location}: true");
        }

        if (input.EquipLevelMin is not null)
        {
            builder.AppendLine($"    EquipLevelMin: {input.EquipLevelMin}");
        }

        if (input.EquipLevelMax is not null)
        {
            builder.AppendLine($"    EquipLevelMax: {input.EquipLevelMax}");
        }

        if (input.ArmorLevel is not null)
        {
            builder.AppendLine($"    ArmorLevel: {input.ArmorLevel}");
        }

        if (input.WeaponLevel is not null)
        {
            builder.AppendLine($"    WeaponLevel: {input.WeaponLevel}");
        }

        if (input.Refineable is not null)
        {
            builder.AppendLine($"    Refineable: {input.Refineable.Value.ToString().ToLowerInvariant()}");
        }

        if (input.ViewId is not null)
        {
            builder.AppendLine($"    View: {input.ViewId}");
        }

        if (!string.IsNullOrWhiteSpace(input.Item.Script))
        {
            builder.AppendLine("    Script: |");
            foreach (var line in SplitScriptLines(input.Item.Script))
            {
                builder.AppendLine($"      {line}");
            }
        }

        if (!string.IsNullOrWhiteSpace(input.EquipScript))
        {
            builder.AppendLine("    EquipScript: |");
            foreach (var line in SplitScriptLines(input.EquipScript))
            {
                builder.AppendLine($"      {line}");
            }
        }

        if (!string.IsNullOrWhiteSpace(input.UnEquipScript))
        {
            builder.AppendLine("    UnEquipScript: |");
            foreach (var line in SplitScriptLines(input.UnEquipScript))
            {
                builder.AppendLine($"      {line}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<string> SplitScriptLines(string script) =>
        script.Replace("\\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd());

    private static string? NormalizeVisualCategory(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "headgear" or "costume-head" or "costume_head" or "accessory" => "headgear",
            "robe" or "garment" => "robe",
            "weapon" => "weapon",
            "shield" => "shield",
            _ => "unsupported"
        };

    private static bool LooksLikeEquipmentType(string itemType) =>
        itemType.Equals("Armor", StringComparison.OrdinalIgnoreCase)
        || itemType.Equals("Weapon", StringComparison.OrdinalIgnoreCase)
        || itemType.Equals("Shadowgear", StringComparison.OrdinalIgnoreCase)
        || itemType.Equals("Ammo", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ResolveVisualDatainfoTargets(string patchPath, string normalizedCategory)
    {
        var datainfoRoot = SafeFileSystem.Combine(patchPath, "data", "luafiles514", "lua files", "datainfo");
        return normalizedCategory switch
        {
            "headgear" => [SafeFileSystem.Combine(datainfoRoot, "accessoryid.lub"), SafeFileSystem.Combine(datainfoRoot, "accname.lub")],
            "robe" => [SafeFileSystem.Combine(datainfoRoot, "spriterobeid.lub"), SafeFileSystem.Combine(datainfoRoot, "spriterobename.lub")],
            "weapon" => [SafeFileSystem.Combine(datainfoRoot, "weapontable.lub")],
            _ => []
        };
    }

    private static string BuildRobeNameAppendBlock(string clientSymbolName, string clientSpriteName)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"RobeNameTable[SPRITE_ROBE_IDs.{clientSymbolName}] = \"{clientSpriteName}\"");
        builder.Append($"RobeNameTable_Eng[SPRITE_ROBE_IDs.{clientSymbolName}] = \"{clientSpriteName}\"");
        return builder.ToString();
    }

    private static IEnumerable<ItemDependency> BuildWeaponDependencies(EquipmentDefinitionInput input)
    {
        if (!input.Item.Type.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Weapon visual category requires item type Weapon.");
        }

        if (!input.EquipLocations.Any(location => location.Equals("Right_Hand", StringComparison.OrdinalIgnoreCase) || location.Equals("Both_Hand", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Weapon visual category requires Right_Hand or Both_Hand equip location.");
        }

        var weaponBaseType = NormalizeWeaponBaseType(input.WeaponBaseType);
        if (weaponBaseType is null)
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Weapon base type is required for Expansion_Weapon_IDs.");
        }
        else if (!KnownWeaponBaseTypes.Contains(weaponBaseType))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Weapon base type '{weaponBaseType}' is not recognized for this client table.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Weapon base type resolved as {weaponBaseType}.");
        }

        if (string.IsNullOrWhiteSpace(input.WeaponHitSound))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Warning, "Weapon hit sound was not provided; dry-run will infer it from the weapon base type.");
        }
        else if (!IsSafeLuaStringLiteral(input.WeaponHitSound) || !input.WeaponHitSound.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Weapon hit sound '{input.WeaponHitSound}' is not a safe .wav Lua string.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Weapon hit sound '{input.WeaponHitSound}' is set.");
        }
    }

    private static IEnumerable<ItemDependency> BuildShieldDependencies(string patchPath, EquipmentDefinitionInput input)
    {
        if (!input.Item.Type.Equals("Armor", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Shield visual category requires item type Armor.");
        }

        if (!input.EquipLocations.Any(location => location.Equals("Left_Hand", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Shield visual category requires Left_Hand equip location.");
        }

        if (input.ViewId is null || !KnownShieldViewIds.Contains(input.ViewId.Value))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, $"Shield visual category only supports built-in client views: {string.Join(", ", KnownShieldViewIds.OrderBy(value => value))}.");
        }
        else
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Satisfied, $"Shield visual uses built-in client view {input.ViewId}; no visual datainfo append is required.");
        }

        if (!string.IsNullOrWhiteSpace(input.ClientSymbolName) || !string.IsNullOrWhiteSpace(input.ClientSpriteName))
        {
            yield return new ItemDependency("Equipment", ItemDependencyState.Missing, "Shield dry-run only supports built-in client views for now; do not provide client symbol or client sprite for custom visual registration.");
        }

        var robeHint = !string.IsNullOrWhiteSpace(input.ClientSpriteName)
            ? FindShieldVisualRobeHint(patchPath, input.ClientSpriteName)
            : null;
        if (robeHint is not null)
        {
            yield return new ItemDependency(
                "Equipment",
                ItemDependencyState.Missing,
                $"Client patch references shield-like visual '{robeHint.Value.SpriteName}' inside robe tables; treat this as robe/Costume_Garment pipeline instead of custom Left_Hand shield registration.",
                robeHint.Value.SourcePath);
        }
    }

    private IEnumerable<ItemDependency> BuildVisualAssetDependencies(
        RepositoryPaths paths,
        EquipmentDefinitionInput input,
        VisualThemeEvaluation? visualTheme,
        GrfAssetLookupResult? grfLookup)
    {
        if (string.IsNullOrWhiteSpace(input.ClientSpriteName) || !IsSafeLuaStringLiteral(input.ClientSpriteName))
        {
            yield break;
        }

        var looseAssetCandidates = FindLooseVisualAssetCandidates(paths.PatchPath, input.ClientSpriteName);
        if (looseAssetCandidates.Count > 0)
        {
            yield return new ItemDependency("Assets", ItemDependencyState.Satisfied, $"Found {looseAssetCandidates.Count} loose visual sprite candidate(s) for '{input.ClientSpriteName}'.", looseAssetCandidates[0]);
            yield break;
        }

        if (grfLookup is { Matches.Count: > 0 })
        {
            var firstMatch = grfLookup.Matches[0];
            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Satisfied,
                $"Found {grfLookup.Matches.Count} GRF visual sprite candidate(s) for '{input.ClientSpriteName}' after scanning {grfLookup.ContainersScanned} container(s) {DescribeLookupSource(grfLookup)}.",
                $"{firstMatch.ContainerPath}::{firstMatch.RelativePath}");
        }
        else if (grfLookup is { Searched: true })
        {
            var themedDependencies = BuildThemeAssistedAssetDependencies(paths, input, visualTheme, grfLookup.ContainersScanned).ToArray();
            if (themedDependencies.Length > 0)
            {
                foreach (var dependency in themedDependencies)
                {
                    yield return dependency;
                }

                yield break;
            }

            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Missing,
                $"No loose patch or GRF visual sprite matched '{input.ClientSpriteName}' after scanning {grfLookup.ContainersScanned} container(s).");
        }
        else
        {
            var themedDependencies = BuildThemeAssistedAssetDependencies(paths, input, visualTheme, containersScanned: null).ToArray();
            if (themedDependencies.Length > 0)
            {
                foreach (var dependency in themedDependencies)
                {
                    yield return dependency;
                }

                yield break;
            }

            yield return new ItemDependency("Assets", ItemDependencyState.Warning, $"No loose visual sprite matched '{input.ClientSpriteName}'. Enable GRF asset lookup to inspect container contents.");
        }
    }

    private IEnumerable<ItemDependency> BuildThemeAssistedAssetDependencies(
        RepositoryPaths paths,
        EquipmentDefinitionInput input,
        VisualThemeEvaluation? visualTheme,
        int? containersScanned)
    {
        if (visualTheme is not { LookupTokens.Count: > 0 })
        {
            yield break;
        }

        var themeLabel = DescribeVisualTheme(visualTheme);
        var category = NormalizeVisualCategory(input.VisualCategory);
        var looseCandidates = FindThemedLooseVisualAssetCandidates(
            paths.PatchPath,
            input.ClientSpriteName!,
            category,
            visualTheme.LookupTokens);

        if (looseCandidates.Count > 0)
        {
            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Warning,
                $"No exact visual sprite matched '{input.ClientSpriteName}', but theme-assisted lookup found {looseCandidates.Count} probable loose candidate(s) for {themeLabel}.",
                looseCandidates[0]);
        }

        var indexedCandidates = FindThemeAssistedIndexedGrfCandidates(input, visualTheme);
        if (indexedCandidates.Count > 0)
        {
            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Warning,
                $"No exact visual sprite matched '{input.ClientSpriteName}', but theme-assisted GRF index lookup found {indexedCandidates.Count} probable candidate(s) for {themeLabel}.",
                indexedCandidates[0]);
            yield break;
        }

        var themedGrfLookup = _grfAssetLookupService?.FindAssets(
            paths,
            input.ClientSpriteName!,
            VisualAssetExtensions,
            _grfAssetLookupOptions with
            {
                AllowContainsMatch = true,
                NameHints = visualTheme.LookupTokens
            });

        if (themedGrfLookup is { Matches.Count: > 0 })
        {
            var firstMatch = themedGrfLookup.Matches[0];
            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Warning,
                $"No exact visual sprite matched '{input.ClientSpriteName}', but theme-assisted GRF lookup found {themedGrfLookup.Matches.Count} probable candidate(s) for {themeLabel} after scanning {themedGrfLookup.ContainersScanned} container(s).",
                $"{firstMatch.ContainerPath}::{firstMatch.RelativePath}");
            yield break;
        }

        if (looseCandidates.Count > 0)
        {
            yield break;
        }

        if (containersScanned is not null)
        {
            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Missing,
                $"No exact or theme-assisted visual sprite matched '{input.ClientSpriteName}' for {themeLabel} after scanning {containersScanned} container(s).");
        }
        else
        {
            yield return new ItemDependency(
                "Assets",
                ItemDependencyState.Warning,
                $"No exact or theme-assisted loose visual sprite matched '{input.ClientSpriteName}' for {themeLabel}. Enable GRF asset lookup to inspect container contents.");
        }
    }

    private static IEnumerable<ItemDependency> BuildVisualDuplicateDependencies(
        string datainfoPath,
        string category,
        EquipmentDefinitionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.ClientSymbolName) || input.ViewId is null or <= 0)
        {
            yield break;
        }

        var text = File.ReadAllText(datainfoPath);
        if (Regex.IsMatch(text, $@"\b{Regex.Escape(input.ClientSymbolName)}\b", RegexOptions.CultureInvariant))
        {
            yield return new ItemDependency("Patch", ItemDependencyState.Missing, $"Visual symbol '{input.ClientSymbolName}' already exists in datainfo.", datainfoPath);
        }

        if (IsVisualIdDeclared(text, category, input.ViewId.Value))
        {
            yield return new ItemDependency("Patch", ItemDependencyState.Missing, $"View ID {input.ViewId} already exists in the visual ID table.", datainfoPath);
        }
    }

    private static bool IsVisualIdDeclared(string text, string category, int viewId)
    {
        var pattern = category switch
        {
            "headgear" => $@"ACCESSORY_IDs\s*=\s*\{{[\s\S]*?=\s*{viewId}\s*(,|\r|\n|\}})",
            "robe" => $@"SPRITE_ROBE_IDs\s*=\s*\{{[\s\S]*?=\s*{viewId}\s*(,|\r|\n|\}})",
            "weapon" => $@"Weapon_IDs\s*=\s*\{{[\s\S]*?=\s*{viewId}\s*(,|\r|\n|\}})",
            _ => ""
        };

        return pattern.Length > 0 && Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
    }

    private static IReadOnlyList<string> FindLooseVisualAssetCandidates(string patchPath, string clientSpriteName)
    {
        var dataPath = SafeFileSystem.Combine(patchPath, "data");
        if (!Directory.Exists(dataPath))
        {
            return [];
        }

        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            clientSpriteName,
            clientSpriteName.TrimStart('_')
        };
        var matches = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories))
            {
                if (matches.Count >= 10)
                {
                    break;
                }

                var extension = Path.GetExtension(file);
                if (!VisualAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(file);
                if (normalizedNames.Contains(name))
                {
                    matches.Add(file);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }

        return matches;
    }

    private static IReadOnlyList<string> FindThemedLooseVisualAssetCandidates(
        string patchPath,
        string clientSpriteName,
        string? normalizedCategory,
        IReadOnlyList<string> lookupTokens)
    {
        var dataPath = SafeFileSystem.Combine(patchPath, "data");
        if (!Directory.Exists(dataPath))
        {
            return [];
        }

        var normalizedClientSpriteName = NormalizeLookupToken(clientSpriteName.TrimStart('_'));
        var normalizedCategoryHint = NormalizeLookupToken(normalizedCategory ?? string.Empty);
        var tokens = lookupTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tokens.Length == 0)
        {
            return [];
        }

        var matches = new List<(string Path, int Score)>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (!VisualAssetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalizedName = NormalizeLookupToken(Path.GetFileNameWithoutExtension(file));
                var normalizedPath = NormalizeLookupToken(file);
                var score = tokens.Count(token => normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (score == 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(normalizedCategoryHint)
                    && normalizedPath.Contains(normalizedCategoryHint, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1;
                }

                if (normalizedClientSpriteName.Length >= 4
                    && normalizedName.Contains(normalizedClientSpriteName[..Math.Min(normalizedClientSpriteName.Length, 8)], StringComparison.OrdinalIgnoreCase))
                {
                    score += 1;
                }

                matches.Add((file, score));
            }
        }
        catch (UnauthorizedAccessException)
        {
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Path.Length)
            .ThenBy(match => match.Path, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(match => match.Path)
            .ToArray();
    }

    private IReadOnlyList<string> FindThemeAssistedIndexedGrfCandidates(
        EquipmentDefinitionInput input,
        VisualThemeEvaluation visualTheme)
    {
        if (_grfContainerIndexStore is null || _grfAssetLookupOptions.ContainerPaths.Count == 0)
        {
            return [];
        }

        var normalizedCategoryHint = NormalizeLookupToken(NormalizeVisualCategory(input.VisualCategory) ?? string.Empty);
        var normalizedClientSpriteName = NormalizeLookupToken((input.ClientSpriteName ?? string.Empty).TrimStart('_'));
        var lookupTokens = visualTheme.LookupTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (lookupTokens.Length == 0)
        {
            return [];
        }

        var results = new List<(string SourcePath, int Score)>();
        foreach (var containerPath in _grfAssetLookupOptions.ContainerPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var indexPath = _grfContainerIndexStore.BuildDefaultIndexPath(containerPath);
            var document = _grfContainerIndexStore.TryLoad(indexPath);
            if (document is null)
            {
                continue;
            }

            foreach (var entry in document.Entries)
            {
                if (!VisualAssetExtensions.Contains(entry.Extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalizedName = NormalizeLookupToken(Path.GetFileNameWithoutExtension(entry.FileName));
                var normalizedRelativePath = NormalizeLookupToken(entry.RelativePath);
                var score = lookupTokens.Count(token => normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (score == 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(normalizedCategoryHint)
                    && normalizedRelativePath.Contains(normalizedCategoryHint, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1;
                }

                if (normalizedClientSpriteName.Length >= 4
                    && normalizedName.Contains(normalizedClientSpriteName[..Math.Min(normalizedClientSpriteName.Length, 8)], StringComparison.OrdinalIgnoreCase))
                {
                    score += 1;
                }

                results.Add(($"{document.ContainerPath}::{entry.RelativePath.Replace('\\', '/')}", score));
            }

            if (!document.IsTruncated && results.Count > 0)
            {
                break;
            }
        }

        return results
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.SourcePath.Length)
            .ThenBy(result => result.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(result => result.SourcePath)
            .ToArray();
    }

    private static string DescribeVisualTheme(VisualThemeEvaluation visualTheme)
    {
        if (visualTheme.SelectedTheme is not null)
        {
            return $"theme '{visualTheme.SelectedTheme.DisplayName}'";
        }

        if (visualTheme.SuggestedThemes.Count > 0)
        {
            return $"suggested theme '{visualTheme.SuggestedThemes[0].DisplayName}'";
        }

        return "theme tokens";
    }

    private static (string SpriteName, string SourcePath)? FindShieldVisualRobeHint(string patchPath, string? clientSpriteName)
    {
        var robeNamePath = SafeFileSystem.Combine(patchPath, "data", "luafiles514", "lua files", "datainfo", "spriterobename.lub");
        if (!File.Exists(robeNamePath))
        {
            return null;
        }

        var text = File.ReadAllText(robeNamePath);
        if (!string.IsNullOrWhiteSpace(clientSpriteName)
            && Regex.IsMatch(text, $@"\b{Regex.Escape(clientSpriteName)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return (clientSpriteName, robeNamePath);
        }

        var genericShieldMatch = Regex.Match(text, "\"([^\"]*shield[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (genericShieldMatch.Success)
        {
            return (genericShieldMatch.Groups[1].Value, robeNamePath);
        }

        return null;
    }

    private GrfAssetLookupResult? ResolveVisualAssetLookup(
        RepositoryPaths paths,
        EquipmentDefinitionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.ClientSpriteName) || !IsSafeLuaStringLiteral(input.ClientSpriteName))
        {
            return null;
        }

        return _grfAssetLookupService?.FindAssets(
            paths,
            input.ClientSpriteName,
            VisualAssetExtensions,
            _grfAssetLookupOptions);
    }

    private static string DescribeLookupSource(GrfAssetLookupResult lookup) =>
        lookup.Source switch
        {
            GrfAssetLookupSource.LocalIndex => lookup.LocalIndexesLoaded > 0
                ? $"via local index ({lookup.LocalIndexesLoaded} loaded)"
                : "via local index",
            GrfAssetLookupSource.LiveScanFallback => lookup.LocalIndexesLoaded > 0
                ? $"via live scan fallback after checking {lookup.LocalIndexesLoaded} local index(es)"
                : "via live scan fallback",
            GrfAssetLookupSource.LiveScan => "via live scan",
            _ => "during GRF lookup"
        };

    private static bool IsSafeLuaIdentifier(string value) =>
        Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    private static bool IsSafeLuaStringLiteral(string value) =>
        value.IndexOfAny(['"', '\\', '\r', '\n']) < 0;

    private static string NormalizeLookupToken(string value) =>
        new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static bool ClientSymbolMatchesCategory(string category, string clientSymbolName) =>
        category switch
        {
            "headgear" => clientSymbolName.StartsWith("ACCESSORY_", StringComparison.OrdinalIgnoreCase),
            "robe" => clientSymbolName.StartsWith("ROBE_", StringComparison.OrdinalIgnoreCase),
            "weapon" => clientSymbolName.StartsWith("WEAPONTYPE_", StringComparison.OrdinalIgnoreCase)
                || clientSymbolName.StartsWith("WPCLASS_", StringComparison.OrdinalIgnoreCase),
            _ => true
        };

    private static string? NormalizeWeaponBaseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim()
            .Replace("Weapon_IDs.", "", StringComparison.OrdinalIgnoreCase)
            .Replace('-', '_')
            .Replace(' ', '_')
            .ToUpperInvariant();
        if (!normalized.StartsWith("WEAPONTYPE_", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith("WPCLASS_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "WEAPONTYPE_" + normalized;
        }

        return IsSafeLuaIdentifier(normalized) ? normalized : null;
    }

    private static string BuildWeaponAppendBlock(EquipmentDefinitionInput input, string weaponBaseType)
    {
        var hitSound = ResolveWeaponHitSound(input.WeaponHitSound, weaponBaseType);
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"Weapon_IDs.{input.ClientSymbolName} = {input.ViewId}");
        builder.AppendLine($"WeaponNameTable[Weapon_IDs.{input.ClientSymbolName}] = \"{input.ClientSpriteName}\"");
        builder.AppendLine($"Expansion_Weapon_IDs[Weapon_IDs.{input.ClientSymbolName}] = Weapon_IDs.{weaponBaseType}");
        builder.AppendLine($"WeaponHitWaveNameTable[Weapon_IDs.{input.ClientSymbolName}] = \"{hitSound}\"");
        if (weaponBaseType.Equals("WEAPONTYPE_BOW", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append($"table.insert(BowTypeList, Weapon_IDs.{input.ClientSymbolName})");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveWeaponHitSound(string? explicitHitSound, string weaponBaseType)
    {
        if (!string.IsNullOrWhiteSpace(explicitHitSound))
        {
            return explicitHitSound.Trim();
        }

        return weaponBaseType switch
        {
            "WEAPONTYPE_SHORTSWORD" or "WEAPONTYPE_SWORD" or "WEAPONTYPE_TWOHANDSWORD" => "_hit_sword.wav",
            "WEAPONTYPE_SPEAR" or "WEAPONTYPE_TWOHANDSPEAR" => "_hit_spear.wav",
            "WEAPONTYPE_AXE" or "WEAPONTYPE_TWOHANDAXE" => "_hit_axe.wav",
            "WEAPONTYPE_ROD" or "WPCLASS_TWOHANDROD" => "_hit_rod.wav",
            "WEAPONTYPE_BOW" => "_hit_arrow.wav",
            _ => "_hit_mace.wav"
        };
    }
}
