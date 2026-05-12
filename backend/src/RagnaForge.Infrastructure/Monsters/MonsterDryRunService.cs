using System.Text;
using System.Text.RegularExpressions;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Monsters;
using RagnaForge.Infrastructure;
using RagnaForge.Infrastructure.FileSystem;
using RagnaForge.Infrastructure.Items;

namespace RagnaForge.Infrastructure.Monsters;

public sealed partial class MonsterDryRunService
{
    private static readonly string[] MobDatabaseFiles =
    [
        "db/pre-re/mob_db.yml",
        "db/re/mob_db.yml",
        "db/import/mob_db.yml"
    ];

    private static readonly string[] ItemDatabaseFiles =
    [
        "db/item_db.yml",
        "db/pre-re/item_db.yml",
        "db/pre-re/item_db_equip.yml",
        "db/pre-re/item_db_etc.yml",
        "db/pre-re/item_db_usable.yml",
        "db/re/item_db.yml",
        "db/re/item_db_equip.yml",
        "db/re/item_db_etc.yml",
        "db/re/item_db_usable.yml",
        "db/import/item_db.yml"
    ];

    private static readonly string[] SkillDatabaseFiles =
    [
        "db/skill_db.yml",
        "db/pre-re/skill_db.yml",
        "db/re/skill_db.yml",
        "db/import/skill_db.yml"
    ];

    private static readonly HashSet<string> SupportedSkillStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "any", "idle", "walk", "dead", "loot", "attack", "angry", "chase", "follow", "anytarget"
    };

    private static readonly HashSet<string> SupportedSkillTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "target", "self", "friend", "master", "randomtarget",
        "around", "around1", "around2", "around3", "around4", "around5", "around6", "around7", "around8"
    };

    private static readonly HashSet<string> SupportedSkillConditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "always", "onspawn", "myhpltmaxrate", "myhpinrate", "mystatuson", "mystatusoff",
        "friendhpltmaxrate", "friendhpinrate", "friendstatuson", "friendstatusoff",
        "attackpcgt", "attackpcge", "slavelt", "slavele", "closedattacked", "longrangeattacked",
        "skillused", "afterskill", "casttargeted", "rudeattacked", "mobnearbygt", "groundattacked",
        "damagedgt", "alchemist", "trickcasting"
    };

    public MonsterDryRunReport Create(RepositoryPaths paths, EpisodeProfile episodeProfile, MonsterDefinitionInput input)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(episodeProfile);
        ArgumentNullException.ThrowIfNull(input);

        var dependencies = new List<ItemDependency>();
        var proposedChanges = new List<ProposedFileChange>();
        var warnings = new List<string>();
        var validationWarnings = new List<string>();
        var validationErrors = new List<string>();
        var unsupportedFields = new List<string>();

        var existingMobs = ReadExistingMobs(paths.RathenaPath);
        var existingItems = ReadExistingItems(paths.RathenaPath);
        var existingSkillIds = ReadExistingSkillIds(paths.RathenaPath);
        var existingSkillAnchors = ReadExistingSkillAnchors(paths.RathenaPath);
        var existingSpawnLines = ReadExistingSpawnLines(paths.RathenaPath);

        var resolvedId = input.Id ?? AllocateId(existingMobs.Ids, 40000);
        if (input.Id is null)
        {
            warnings.Add($"Monster ID was not provided; suggested free ID {resolvedId} will be used in the dry-run.");
        }

        var mobDbPath = SafeFileSystem.Combine(paths.RathenaPath, "db", "import", "mob_db.yml");
        var mobAvailPath = SafeFileSystem.Combine(paths.RathenaPath, "db", "import", "mob_avail.yml");
        var mobSkillDbPath = SafeFileSystem.Combine(paths.RathenaPath, "db", "import", "mob_skill_db.txt");
        var spawnScriptPath = SafeFileSystem.Combine(paths.RathenaPath, "npc", "custom", BuildFileName(input));
        var scriptsCustomPath = SafeFileSystem.Combine(paths.RathenaPath, "npc", "scripts_custom.conf");

        dependencies.Add(File.Exists(mobDbPath)
            ? new ItemDependency("rAthena", ItemDependencyState.Satisfied, "db/import/mob_db.yml is available.", mobDbPath)
            : new ItemDependency("rAthena", ItemDependencyState.Missing, "db/import/mob_db.yml was not found.", mobDbPath));

        var normalizedSkills = NormalizeSkills(input);
        if (normalizedSkills.Count > 0)
        {
            dependencies.Add(File.Exists(mobSkillDbPath)
                ? new ItemDependency("rAthena", ItemDependencyState.Satisfied, "db/import/mob_skill_db.txt is available for custom monster skills.", mobSkillDbPath)
                : new ItemDependency("rAthena", ItemDependencyState.Missing, "db/import/mob_skill_db.txt was not found; monster skill proposals are blocked.", mobSkillDbPath));
        }
        else
        {
            dependencies.Add(File.Exists(mobSkillDbPath)
                ? new ItemDependency("rAthena", ItemDependencyState.Satisfied, "db/import/mob_skill_db.txt is available.", mobSkillDbPath)
                : new ItemDependency("rAthena", ItemDependencyState.Warning, "db/import/mob_skill_db.txt was not found; skill proposals remain unavailable until it exists.", mobSkillDbPath));
        }

        if (!string.IsNullOrWhiteSpace(input.SpriteOverride))
        {
            dependencies.Add(File.Exists(mobAvailPath)
                ? new ItemDependency("rAthena", ItemDependencyState.Satisfied, "db/import/mob_avail.yml is available for sprite overrides.", mobAvailPath)
                : new ItemDependency("rAthena", ItemDependencyState.Missing, "db/import/mob_avail.yml was not found; sprite override proposals are blocked.", mobAvailPath));
        }

        if (existingMobs.Ids.Contains(resolvedId))
        {
            dependencies.Add(new ItemDependency("Monster", ItemDependencyState.Missing, $"Monster ID {resolvedId} already exists."));
        }
        else
        {
            dependencies.Add(new ItemDependency("Monster", ItemDependencyState.Satisfied, $"Monster ID {resolvedId} is currently free."));
        }

        if (existingMobs.AegisNames.Contains(input.AegisName))
        {
            dependencies.Add(new ItemDependency("Monster", ItemDependencyState.Missing, $"Monster AegisName '{input.AegisName}' already exists."));
        }
        else
        {
            dependencies.Add(new ItemDependency("Monster", ItemDependencyState.Satisfied, $"Monster AegisName '{input.AegisName}' is currently free."));
        }

        if (File.Exists(spawnScriptPath))
        {
            dependencies.Add(new ItemDependency("Monster", ItemDependencyState.Missing, "Target monster spawn script file already exists.", spawnScriptPath));
        }
        else
        {
            dependencies.Add(new ItemDependency("Monster", ItemDependencyState.Satisfied, "Target monster spawn script file is free.", spawnScriptPath));
        }

        var dropPlans = BuildDropPlans(input, existingItems, validationWarnings, validationErrors, unsupportedFields);
        var skillPlans = BuildSkillPlans(input, existingSkillIds, existingSkillAnchors, validationWarnings, validationErrors, unsupportedFields);
        var spawnPlans = BuildSpawnPlans(paths.RathenaPath, input, existingSpawnLines, validationWarnings, validationErrors);

        foreach (var mapName in spawnPlans.Select(plan => plan.MapName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!MapExists(paths.RathenaPath, mapName))
            {
                dependencies.Add(new ItemDependency("Map", ItemDependencyState.Missing, $"Map '{mapName}' was not found in rAthena map registration files."));
            }
            else
            {
                dependencies.Add(new ItemDependency("Map", ItemDependencyState.Satisfied, $"Map '{mapName}' is registered in rAthena."));
            }
        }

        var loaderLine = $"npc: npc/custom/{BuildFileName(input)}";
        if (File.Exists(scriptsCustomPath)
            && File.ReadAllText(scriptsCustomPath).Contains(loaderLine, StringComparison.OrdinalIgnoreCase))
        {
            validationWarnings.Add($"scripts_custom.conf already references {BuildFileName(input)}.");
        }
        else
        {
            proposedChanges.Add(new ProposedFileChange(
                scriptsCustomPath,
                "append",
                File.Exists(scriptsCustomPath),
                loaderLine));
        }

        proposedChanges.Add(new ProposedFileChange(
            mobDbPath,
            "append",
            File.Exists(mobDbPath),
            BuildMobDbSnippet(resolvedId, input, dropPlans)));

        if (!string.IsNullOrWhiteSpace(input.SpriteOverride) && File.Exists(mobAvailPath))
        {
            proposedChanges.Add(new ProposedFileChange(
                mobAvailPath,
                "append",
                true,
                BuildMobAvailSnippet(input.AegisName, input.SpriteOverride!)));
        }

        if (skillPlans.Count > 0 && File.Exists(mobSkillDbPath))
        {
            proposedChanges.Add(new ProposedFileChange(
                mobSkillDbPath,
                "append",
                true,
                BuildMobSkillSnippet(resolvedId, input, skillPlans)));
        }

        proposedChanges.Add(new ProposedFileChange(
            spawnScriptPath,
            "create",
            File.Exists(spawnScriptPath),
            BuildSpawnScript(resolvedId, spawnPlans)));

        dependencies.Add(new ItemDependency("MonsterSkill", ItemDependencyState.Satisfied, "Monster skill DB format detected as TXT with the classic 19-column layout.", mobSkillDbPath));
        dependencies.Add(new ItemDependency("DryRun", ItemDependencyState.Proposed, $"Prepared {proposedChanges.Count} proposed file change(s) for monster diff preview."));

        warnings.AddRange(validationWarnings.Where(message => !warnings.Contains(message, StringComparer.OrdinalIgnoreCase)));
        var postWritePlan = BuildPostWriteValidationPlan(proposedChanges);

        var readiness = DetermineReadiness(dependencies, validationWarnings, validationErrors, unsupportedFields);
        var canApply = readiness != MonsterApplyReadiness.Blocked;
        var diffPreview = ItemDiffPreviewBuilder.Build(proposedChanges);

        return new MonsterDryRunReport(
            DateTimeOffset.UtcNow,
            episodeProfile,
            input,
            resolvedId,
            canApply,
            dependencies,
            proposedChanges,
            diffPreview,
            warnings,
            dropPlans,
            skillPlans,
            spawnPlans,
            unsupportedFields,
            validationWarnings,
            validationErrors,
            readiness,
            postWritePlan);
    }

    private static IReadOnlyList<MonsterDropPlan> BuildDropPlans(
        MonsterDefinitionInput input,
        (Dictionary<int, string> ById, Dictionary<string, int> ByAegisName) existingItems,
        ICollection<string> validationWarnings,
        ICollection<string> validationErrors,
        ICollection<string> unsupportedFields)
    {
        var plans = new List<MonsterDropPlan>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var drop in input.Drops ?? [])
        {
            var itemReference = drop.ItemAegisName ?? (drop.ItemId?.ToString() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(itemReference))
            {
                validationErrors.Add("Monster drop is missing both Item ID and Item AegisName.");
                continue;
            }

            if (drop.Chance < 1 || drop.Chance > 10000)
            {
                validationErrors.Add($"Drop '{itemReference}' uses chance {drop.Chance}; valid range is 1..10000.");
            }

            var quantity = drop.Quantity ?? 1;
            if (quantity < 1)
            {
                validationErrors.Add($"Drop '{itemReference}' uses quantity {quantity}; quantity must be at least 1.");
            }
            else if (quantity != 1)
            {
                unsupportedFields.Add($"Drop '{itemReference}' requested quantity {quantity}, but MOB_DB Version 5 does not expose stack quantity per drop entry.");
            }

            if (!string.IsNullOrWhiteSpace(drop.Kind)
                && !drop.Kind.Equals("stealprotected", StringComparison.OrdinalIgnoreCase))
            {
                unsupportedFields.Add($"Drop '{itemReference}' uses unsupported kind '{drop.Kind}'. Supported kind in this milestone: stealprotected.");
            }

            int? resolvedItemId = null;
            string? resolvedAegisName = null;
            var exists = true;
            string source;

            if (drop.ItemId is int itemId)
            {
                resolvedItemId = itemId;
                if (existingItems.ById.TryGetValue(itemId, out var aegisName))
                {
                    resolvedAegisName = aegisName;
                    source = "item-id";
                }
                else if (input.AllowFutureDropReferences)
                {
                    exists = false;
                    source = "future-reference";
                    validationWarnings.Add($"Drop item ID {itemId} is not present in the current item DB scan; keep the monster blocked until the future item is introduced with an AegisName.");
                    validationErrors.Add($"Drop item ID {itemId} is unresolved and cannot be emitted safely into mob_db.yml without a matching AegisName.");
                }
                else
                {
                    exists = false;
                    source = "missing";
                    validationErrors.Add($"Drop item ID {itemId} was not found in the current item DB scan.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(drop.ItemAegisName))
            {
                resolvedAegisName = drop.ItemAegisName!.Trim();
                if (existingItems.ByAegisName.TryGetValue(resolvedAegisName, out var parsedAegisItemId))
                {
                    resolvedItemId = parsedAegisItemId;
                    source = "aegis";
                }
                else if (input.AllowFutureDropReferences)
                {
                    exists = false;
                    source = "future-reference";
                    validationWarnings.Add($"Drop item '{resolvedAegisName}' is being kept as a future reference; apply will only be ready after the item exists server-side.");
                }
                else
                {
                    exists = false;
                    source = "missing";
                    validationErrors.Add($"Drop item '{resolvedAegisName}' was not found in the current item DB scan.");
                }
            }
            else
            {
                exists = false;
                source = "missing";
                validationErrors.Add($"Drop '{itemReference}' is not resolvable.");
            }

            var duplicateKey = $"{(drop.IsMvp ? "mvp" : "drop")}|{resolvedAegisName ?? itemReference}";
            if (!seenKeys.Add(duplicateKey))
            {
                validationErrors.Add($"Drop '{itemReference}' is duplicated in the same monster plan.");
            }

            plans.Add(new MonsterDropPlan(
                itemReference,
                resolvedItemId,
                resolvedAegisName,
                drop.Chance,
                quantity,
                drop.IsMvp,
                drop.Kind,
                exists,
                source));
        }

        return plans;
    }

    private static IReadOnlyList<MonsterSkillPlan> BuildSkillPlans(
        MonsterDefinitionInput input,
        HashSet<int> existingSkillIds,
        HashSet<string> existingSkillAnchors,
        ICollection<string> validationWarnings,
        ICollection<string> validationErrors,
        ICollection<string> unsupportedFields)
    {
        var skills = NormalizeSkills(input);
        var plans = new List<MonsterSkillPlan>();
        var seenAnchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            var notes = new List<string>();
            var anchor = string.IsNullOrWhiteSpace(skill.Anchor)
                ? $"{input.AegisName}@RagnaForge_{index + 1:D2}"
                : skill.Anchor!.Trim();

            if (anchor.Contains(',', StringComparison.Ordinal) || anchor.Contains('\n'))
            {
                validationErrors.Add($"Skill anchor '{anchor}' is not safe for mob_skill_db.txt.");
            }

            if (!seenAnchors.Add(anchor))
            {
                validationErrors.Add($"Skill anchor '{anchor}' is duplicated in the same monster plan.");
            }

            if (existingSkillAnchors.Contains(anchor))
            {
                validationErrors.Add($"Skill anchor '{anchor}' already exists in mob_skill_db.txt.");
            }

            if (!existingSkillIds.Contains(skill.SkillId))
            {
                validationErrors.Add($"Skill ID {skill.SkillId} was not found in skill_db.yml.");
            }

            if (skill.SkillLevel < 1)
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses level {skill.SkillLevel}; level must be at least 1.");
            }

            if (skill.Rate < 0 || skill.Rate > 10000)
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses rate {skill.Rate}; valid range is 0..10000.");
            }

            if (skill.CastTimeMilliseconds < 0 || skill.DelayMilliseconds < 0)
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses negative cast/delay values, which are not supported.");
            }

            if (!SupportedSkillStates.Contains(skill.State))
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses unsupported state '{skill.State}'.");
            }

            if (!SupportedSkillTargets.Contains(skill.Target))
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses unsupported target '{skill.Target}'.");
            }

            if (!SupportedSkillConditions.Contains(skill.ConditionType))
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses unsupported condition '{skill.ConditionType}'.");
            }

            if (skill.UnsupportedFields is { Count: > 0 })
            {
                foreach (var pair in skill.UnsupportedFields)
                {
                    unsupportedFields.Add($"Skill ID {skill.SkillId} received unsupported field '{pair.Key}' with value '{pair.Value}'.");
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.Chat) && (skill.Chat.Contains(',', StringComparison.Ordinal) || skill.Chat.Contains('\n')))
            {
                validationErrors.Add($"Skill ID {skill.SkillId} uses a Chat value that is not safe for comma-separated mob_skill_db.txt.");
            }

            var signature = $"{skill.SkillId}|{skill.SkillLevel}|{skill.State}|{skill.Target}|{skill.ConditionType}|{skill.ConditionValue}|{anchor}";
            if (!seenSignatures.Add(signature))
            {
                validationErrors.Add($"Skill ID {skill.SkillId} is duplicated with the same anchor/signature.");
            }

            if (skill.ConditionType.Equals("myhpinrate", StringComparison.OrdinalIgnoreCase)
                || skill.ConditionType.Equals("friendhpinrate", StringComparison.OrdinalIgnoreCase))
            {
                if (skill.Value1 is null)
                {
                    validationErrors.Add($"Skill ID {skill.SkillId} uses condition '{skill.ConditionType}' and must provide val1 as the upper bound.");
                }
                else
                {
                    notes.Add($"Range condition upper bound uses val1={skill.Value1.Value}.");
                }
            }

            if (skill.ConditionType.Equals("always", StringComparison.OrdinalIgnoreCase)
                && skill.ConditionValue != 0)
            {
                validationWarnings.Add($"Skill ID {skill.SkillId} uses condition 'always' with non-zero ConditionValue {skill.ConditionValue}; rAthena usually ignores it.");
            }

            plans.Add(new MonsterSkillPlan(
                skill.SkillId,
                skill.SkillLevel,
                skill.State,
                skill.Target,
                skill.ConditionType,
                skill.ConditionValue,
                anchor,
                skill.UnsupportedFields is null || skill.UnsupportedFields.Count == 0,
                notes));
        }

        return plans;
    }

    private static IReadOnlyList<MonsterSpawnPlan> BuildSpawnPlans(
        string rathenaPath,
        MonsterDefinitionInput input,
        HashSet<string> existingSpawnLines,
        ICollection<string> validationWarnings,
        ICollection<string> validationErrors)
    {
        var plans = new List<MonsterSpawnPlan>();
        var seenSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var spawn in NormalizeSpawns(input))
        {
            var mapName = string.IsNullOrWhiteSpace(spawn.MapName) ? input.MapName : spawn.MapName!.Trim();
            var amount = spawn.Amount ?? input.Amount;
            var respawnMilliseconds = spawn.RespawnMilliseconds ?? input.RespawnMilliseconds;
            var label = string.IsNullOrWhiteSpace(spawn.Label) ? input.DisplayName : spawn.Label!.Trim();
            var notes = new List<string>();

            if (!MapExists(rathenaPath, mapName))
            {
                validationErrors.Add($"Spawn map '{mapName}' is not registered in rAthena.");
            }

            if (spawn.X < 0 || spawn.Y < 0 || spawn.AreaX < 0 || spawn.AreaY < 0)
            {
                validationErrors.Add($"Spawn '{label}' uses negative coordinates or area, which is not allowed.");
            }

            if (amount < 1)
            {
                validationErrors.Add($"Spawn '{label}' uses amount {amount}; amount must be at least 1.");
            }

            if (respawnMilliseconds < 0)
            {
                validationErrors.Add($"Spawn '{label}' uses respawn {respawnMilliseconds}; respawn must be 0 or greater.");
            }

            if (!string.IsNullOrWhiteSpace(spawn.EventLabel))
            {
                notes.Add($"Spawn will call event '{spawn.EventLabel}'.");
            }

            if (spawn.Randomize || (spawn.X == 0 && spawn.Y == 0 && spawn.AreaX == 0 && spawn.AreaY == 0))
            {
                notes.Add("Spawn is effectively map-random because coordinates/area are zeroed.");
            }

            var plan = new MonsterSpawnPlan(
                mapName,
                spawn.X,
                spawn.Y,
                spawn.AreaX,
                spawn.AreaY,
                amount,
                respawnMilliseconds,
                label,
                spawn.EventLabel,
                spawn.Randomize,
                notes);

            var signature = BuildSpawnLine(0, plan);
            if (!seenSignatures.Add(signature))
            {
                validationErrors.Add($"Spawn '{label}' is duplicated in the same monster plan.");
            }

            if (existingSpawnLines.Contains(signature))
            {
                validationErrors.Add($"Spawn '{label}' already exists in npc/custom with the same line signature.");
            }

            plans.Add(plan);
        }

        return plans;
    }

    private static IReadOnlyList<MonsterSkillDefinition> NormalizeSkills(MonsterDefinitionInput input)
    {
        var skills = new List<MonsterSkillDefinition>();
        if (input.Skill is not null)
        {
            skills.Add(input.Skill);
        }

        if (input.Skills is { Count: > 0 })
        {
            skills.AddRange(input.Skills);
        }

        return skills;
    }

    private static IReadOnlyList<MonsterSpawnDefinition> NormalizeSpawns(MonsterDefinitionInput input)
    {
        var spawns = new List<MonsterSpawnDefinition>
        {
            input.Spawn
        };

        if (input.Spawns is { Count: > 0 })
        {
            spawns.AddRange(input.Spawns);
        }

        return spawns;
    }

    private static MonsterApplyReadiness DetermineReadiness(
        IReadOnlyList<ItemDependency> dependencies,
        IReadOnlyList<string> validationWarnings,
        IReadOnlyList<string> validationErrors,
        IReadOnlyList<string> unsupportedFields)
    {
        if (dependencies.Any(item => item.State == ItemDependencyState.Missing)
            || validationErrors.Count > 0
            || unsupportedFields.Count > 0)
        {
            return MonsterApplyReadiness.Blocked;
        }

        return validationWarnings.Count > 0 || dependencies.Any(item => item.State == ItemDependencyState.Warning)
            ? MonsterApplyReadiness.ReadyWithWarnings
            : MonsterApplyReadiness.Ready;
    }

    private static IReadOnlyList<PostWriteValidationPlanEntry> BuildPostWriteValidationPlan(IReadOnlyList<ProposedFileChange> proposedChanges) =>
        proposedChanges.Select(change =>
        {
            var fileName = Path.GetFileName(change.TargetPath);
            var validatorName = fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                                || fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                ? nameof(YamlSyntaxValidator)
                : fileName.Equals("mob_skill_db.txt", StringComparison.OrdinalIgnoreCase)
                    ? nameof(RathenaTxtValidator)
                    : fileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)
                      || fileName.EndsWith(".lub", StringComparison.OrdinalIgnoreCase)
                        ? nameof(LuaTextValidator)
                        : nameof(RathenaScriptValidator);

            return new PostWriteValidationPlanEntry(
                change.TargetPath,
                validatorName,
                "Validate the fully assembled staging file before the final replacement.");
        }).ToArray();

    private static string BuildMobDbSnippet(int resolvedId, MonsterDefinitionInput input, IReadOnlyList<MonsterDropPlan> dropPlans)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("# RagnaForge monster dry-run proposal");
        builder.AppendLine($"  - Id: {resolvedId}");
        builder.AppendLine($"    AegisName: {input.AegisName}");
        builder.AppendLine($"    Name: {input.DisplayName}");
        builder.AppendLine($"    Level: {input.Level}");
        builder.AppendLine($"    Hp: {input.Hp}");
        builder.AppendLine("    Sp: 0");
        builder.AppendLine("    Attack: 10");
        builder.AppendLine("    Attack2: 12");
        builder.AppendLine("    Defense: 0");
        builder.AppendLine("    MagicDefense: 0");
        builder.AppendLine("    AttackRange: 1");
        builder.AppendLine("    SkillRange: 1");
        builder.AppendLine("    ChaseRange: 12");
        builder.AppendLine("    Size: Medium");
        builder.AppendLine("    Race: Formless");
        builder.AppendLine("    Element: Neutral");
        builder.AppendLine("    ElementLevel: 1");
        builder.AppendLine("    Ai: 06");

        var mvpDrops = dropPlans.Where(drop => drop.IsMvp && (!string.IsNullOrWhiteSpace(drop.ResolvedAegisName) || drop.Source.Equals("future-reference", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (mvpDrops.Length > 0)
        {
            builder.AppendLine("    MvpDrops:");
            foreach (var drop in mvpDrops)
            {
                builder.AppendLine($"      - Item: {drop.ResolvedAegisName ?? drop.ItemReference}");
                builder.AppendLine($"        Rate: {drop.Chance}");
            }
        }

        var normalDrops = dropPlans.Where(drop => !drop.IsMvp && (!string.IsNullOrWhiteSpace(drop.ResolvedAegisName) || drop.Source.Equals("future-reference", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (normalDrops.Length > 0)
        {
            builder.AppendLine("    Drops:");
            foreach (var drop in normalDrops)
            {
                builder.AppendLine($"      - Item: {drop.ResolvedAegisName ?? drop.ItemReference}");
                builder.AppendLine($"        Rate: {drop.Chance}");
                if (!string.IsNullOrWhiteSpace(drop.Kind)
                    && drop.Kind.Equals("stealprotected", StringComparison.OrdinalIgnoreCase))
                {
                    builder.AppendLine("        StealProtected: true");
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildMobAvailSnippet(string aegisName, string spriteOverride) =>
        $"\n# RagnaForge monster dry-run proposal\n  - Mob: {aegisName}\n    Sprite: {spriteOverride}";

    private static string BuildMobSkillSnippet(int resolvedId, MonsterDefinitionInput input, IReadOnlyList<MonsterSkillPlan> skillPlans)
    {
        var skills = NormalizeSkills(input);
        var builder = new StringBuilder();
        for (var index = 0; index < skillPlans.Count && index < skills.Count; index++)
        {
            var plan = skillPlans[index];
            var skill = skills[index];
            if (!plan.Supported)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.AppendLine();
            }

            builder.Append(resolvedId).Append(',');
            builder.Append(plan.Anchor).Append(',');
            builder.Append(skill.State).Append(',');
            builder.Append(skill.SkillId).Append(',');
            builder.Append(skill.SkillLevel).Append(',');
            builder.Append(skill.Rate).Append(',');
            builder.Append(skill.CastTimeMilliseconds).Append(',');
            builder.Append(skill.DelayMilliseconds).Append(',');
            builder.Append(skill.Cancelable ? "yes" : "no").Append(',');
            builder.Append(skill.Target).Append(',');
            builder.Append(skill.ConditionType).Append(',');
            builder.Append(skill.ConditionValue).Append(',');
            builder.Append(NullableInt(skill.Value1)).Append(',');
            builder.Append(NullableInt(skill.Value2)).Append(',');
            builder.Append(NullableInt(skill.Value3)).Append(',');
            builder.Append(NullableInt(skill.Value4)).Append(',');
            builder.Append(NullableInt(skill.Value5)).Append(',');
            builder.Append(NullableInt(skill.Emotion)).Append(',');
            builder.Append(skill.Chat ?? string.Empty);
            if (index + 1 < skillPlans.Count && index + 1 < skills.Count)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSpawnScript(int resolvedId, IReadOnlyList<MonsterSpawnPlan> spawnPlans)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < spawnPlans.Count; index++)
        {
            builder.Append(BuildSpawnLine(resolvedId, spawnPlans[index]));
            if (index + 1 < spawnPlans.Count)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string BuildSpawnLine(int resolvedId, MonsterSpawnPlan spawnPlan)
    {
        var builder = new StringBuilder();
        builder.Append(spawnPlan.MapName).Append(',');
        builder.Append(Math.Max(0, spawnPlan.X)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.Y)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.AreaX)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.AreaY)).Append('\t');
        builder.Append("monster\t");
        builder.Append(string.IsNullOrWhiteSpace(spawnPlan.Label) ? "Monster" : spawnPlan.Label).Append('\t');
        builder.Append(resolvedId).Append(',');
        builder.Append(Math.Max(1, spawnPlan.Amount)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.RespawnMilliseconds)).Append(',');
        builder.Append(Math.Max(0, spawnPlan.RespawnMilliseconds));
        if (!string.IsNullOrWhiteSpace(spawnPlan.EventLabel))
        {
            builder.Append(',').Append(spawnPlan.EventLabel);
        }

        return builder.ToString();
    }

    private static string NullableInt(int? value) =>
        value?.ToString() ?? string.Empty;

    private static string BuildFileName(MonsterDefinitionInput input)
    {
        var slug = string.IsNullOrWhiteSpace(input.FileSlug) ? input.AegisName : input.FileSlug!;
        slug = Regex.Replace(slug.Trim().ToLowerInvariant(), @"[^a-z0-9_]+", "_");
        slug = slug.Trim('_');
        return $"ragnaforge_mob_{(slug.Length == 0 ? "custom" : slug)}.txt";
    }

    private static bool MapExists(string rathenaPath, string mapName)
    {
        var mapIndexPath = SafeFileSystem.Combine(rathenaPath, "db", "import", "map_index.txt");
        var mapsAthenaPath = SafeFileSystem.Combine(rathenaPath, "conf", "maps_athena.conf");

        return SafeFileSystem.ReadLinesIfExists(mapIndexPath).Any(line => line.TrimStart().StartsWith(mapName + " ", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals(mapName, StringComparison.OrdinalIgnoreCase))
               || SafeFileSystem.ReadLinesIfExists(mapsAthenaPath).Any(line => line.Trim().Equals($"map: {mapName}", StringComparison.OrdinalIgnoreCase));
    }

    private static (HashSet<int> Ids, HashSet<string> AegisNames) ReadExistingMobs(string rathenaPath)
    {
        var ids = new HashSet<int>();
        var aegisNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in MobDatabaseFiles)
        {
            var fullPath = SafeFileSystem.Combine(rathenaPath, relativePath.Split('/'));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(fullPath))
            {
                var idMatch = MobIdRegex().Match(line);
                if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var id))
                {
                    ids.Add(id);
                }

                var aegisMatch = MobAegisRegex().Match(line);
                if (aegisMatch.Success)
                {
                    aegisNames.Add(aegisMatch.Groups[1].Value.Trim());
                }
            }
        }

        return (ids, aegisNames);
    }

    private static (Dictionary<int, string> ById, Dictionary<string, int> ByAegisName) ReadExistingItems(string rathenaPath)
    {
        var byId = new Dictionary<int, string>();
        var byAegisName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in ItemDatabaseFiles)
        {
            var fullPath = SafeFileSystem.Combine(rathenaPath, relativePath.Split('/'));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            int? currentId = null;
            foreach (var line in File.ReadLines(fullPath))
            {
                var idMatch = ItemIdRegex().Match(line);
                if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var id))
                {
                    currentId = id;
                    continue;
                }

                var aegisMatch = ItemAegisRegex().Match(line);
                if (aegisMatch.Success)
                {
                    var aegisName = aegisMatch.Groups[1].Value.Trim();
                    if (currentId is int itemId)
                    {
                        byId[itemId] = aegisName;
                        byAegisName[aegisName] = itemId;
                    }
                }
            }
        }

        return (byId, byAegisName);
    }

    private static HashSet<int> ReadExistingSkillIds(string rathenaPath)
    {
        var ids = new HashSet<int>();
        foreach (var relativePath in SkillDatabaseFiles)
        {
            var fullPath = SafeFileSystem.Combine(rathenaPath, relativePath.Split('/'));
            if (!File.Exists(fullPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(fullPath))
            {
                var match = SkillIdRegex().Match(line);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    private static HashSet<string> ReadExistingSkillAnchors(string rathenaPath)
    {
        var anchors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mobSkillDbPath = SafeFileSystem.Combine(rathenaPath, "db", "import", "mob_skill_db.txt");
        if (!File.Exists(mobSkillDbPath))
        {
            return anchors;
        }

        foreach (var line in File.ReadLines(mobSkillDbPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var columns = trimmed.Split(',', StringSplitOptions.None);
            if (columns.Length >= 2 && !string.IsNullOrWhiteSpace(columns[1]))
            {
                anchors.Add(columns[1].Trim());
            }
        }

        return anchors;
    }

    private static HashSet<string> ReadExistingSpawnLines(string rathenaPath)
    {
        var lines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npcCustomPath = SafeFileSystem.Combine(rathenaPath, "npc", "custom");
        if (!Directory.Exists(npcCustomPath))
        {
            return lines;
        }

        foreach (var file in Directory.EnumerateFiles(npcCustomPath, "*.txt", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("\tmonster\t", StringComparison.Ordinal))
                {
                    lines.Add(trimmed);
                }
            }
        }

        return lines;
    }

    private static int AllocateId(HashSet<int> existingIds, int start)
    {
        var current = Math.Max(1, start);
        while (existingIds.Contains(current))
        {
            current++;
        }

        return current;
    }

    [GeneratedRegex(@"^\s*-\s+Id:\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex MobIdRegex();

    [GeneratedRegex(@"^\s*AegisName:\s*(\S+)", RegexOptions.Compiled)]
    private static partial Regex MobAegisRegex();

    [GeneratedRegex(@"^\s*-\s+Id:\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex ItemIdRegex();

    [GeneratedRegex(@"^\s*AegisName\s*:\s*(.+?)\s*$", RegexOptions.Compiled)]
    private static partial Regex ItemAegisRegex();

    [GeneratedRegex(@"^\s*-\s+Id:\s*(\d+)", RegexOptions.Compiled)]
    private static partial Regex SkillIdRegex();
}
