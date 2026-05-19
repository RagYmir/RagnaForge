using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Scanning;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge validate'. Read-only validation based on cached indices.
/// Never modifies any file.
/// </summary>
public sealed class ValidateCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _scope; // all, items, npcs, monsters, maps, client, server

    public ValidateCommand(string configDir, string agentRoot, string scope = "all")
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _scope = scope;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("validate");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            var cacheInspection = AgentCacheInspector.InspectEntityIndex(
                _agentRoot,
                pathsConfig.ActiveProfile,
                fingerprint);

            if (cacheInspection.Document is null)
            {
                var cacheIssue = ValidationOperationalClassifier.CreateCacheIssue(
                    "CACHE_ENTITY_INDEX_UNTRUSTED",
                    $"Entity index is not trusted ({cacheInspection.Details.CacheStaleReason ?? "cache_not_found"}).",
                    "Run 'ragnaforge index --entities --json' before relying on validation results.");
                var cacheSummary = ValidationOperationalClassifier.BuildSummary([cacheIssue]);

                output = JsonOutput.Error(
                    "validate",
                    "Entity index not found or obsolete. Run 'ragnaforge index --entities --json' first.");
                output.ActiveProfile = pathsConfig.ActiveProfile;
                output.ConfigFingerprint = fingerprint;
                output.NextRequiredAction = "run_index";
                output.Data = new
                {
                    scope = _scope,
                    totalIssues = 1,
                    errors = 1,
                    warnings = 0,
                    safeForReadOnlyWork = cacheSummary.SafeForReadOnlyWork,
                    safeForDryRun = cacheSummary.SafeForDryRun,
                    safeForApply = cacheSummary.SafeForApply,
                    issueSummaryByScope = cacheSummary.IssueSummaryByScope,
                    issueSummaryByBlockingTarget = cacheSummary.IssueSummaryByBlockingTarget,
                    cache = cacheInspection.Details,
                    issues = new[] { cacheIssue }
                };
                return output;
            }

            var index = cacheInspection.Document;
            var issues = new List<ValidationIssue>();

            if (_scope is "all" or "items" or "server")
            {
                issues.AddRange(ValidateItems(index));
            }

            if (_scope is "all" or "monsters" or "server")
            {
                issues.AddRange(ValidateMonsters(index));
            }

            if (_scope is "all" or "npcs" or "server")
            {
                issues.AddRange(ValidateNpcs(index));
            }

            if (_scope is "all" or "maps" or "client")
            {
                issues.AddRange(ValidateMaps(index));
            }

            ValidationOperationalClassifier.ApplyClassification(issues);
            var decisionSummary = ValidationOperationalClassifier.BuildSummary(issues);
            var errors = issues.Count(i => i.Severity is "error" or "critical");
            var warnings = issues.Count(i => i.Severity == "warning");

            output.Summary = $"Validation completed - {issues.Count} issues ({errors} errors, {warnings} warnings).";
            output.SafeForAutomation = decisionSummary.SafeForReadOnlyWork;

            if (!decisionSummary.SafeForReadOnlyWork)
            {
                output.NextRequiredAction = "review_validation_issues";
            }
            else if (!decisionSummary.SafeForApply)
            {
                output.NextRequiredAction = "review_validation_issues_before_apply";
            }

            output.Data = new
            {
                scope = _scope,
                totalIssues = issues.Count,
                errors,
                warnings,
                safeForReadOnlyWork = decisionSummary.SafeForReadOnlyWork,
                safeForDryRun = decisionSummary.SafeForDryRun,
                safeForApply = decisionSummary.SafeForApply,
                issueSummaryByScope = decisionSummary.IssueSummaryByScope,
                issueSummaryByBlockingTarget = decisionSummary.IssueSummaryByBlockingTarget,
                cache = cacheInspection.Details,
                issues
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("validate", ex.Message);
        }

        return output;
    }

    public static List<ValidationIssue> ValidateItems(EntityIndex index)
    {
        var issues = new List<ValidationIssue>();
        var serverItems = new Dictionary<int, ItemEntry>();

        foreach (var item in index.Items)
        {
            if (item.Id == -1)
            {
                continue;
            }

            if (item.Side == "server")
            {
                if (serverItems.TryGetValue(item.Id, out var existing))
                {
                    if (IsCrossModeBaseDuplicate(existing, item))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "warning",
                            Code = "ITEM_DUPLICATE_ID_CROSS_DB_MODE",
                            Message = $"Item ID {item.Id} exists in both Renewal and Pre-Renewal DBs: '{item.AegisName}' in {item.RelativePath} and '{existing.AegisName}' in {existing.RelativePath}",
                            EntityType = "item",
                            EntityId = item.Id.ToString(),
                            EntityName = item.AegisName,
                            SourceFile = item.SourceFile,
                            Line = item.Line,
                            Recommendation = "Use a non-hybrid dbMode or review whether both DB modes should remain active."
                        });
                        continue;
                    }

                    issues.Add(new ValidationIssue
                    {
                        Severity = "error",
                        Code = "ITEM_DUPLICATE_ID_SERVER",
                        Message = $"Duplicate server item ID {item.Id}: '{item.AegisName}' in {item.RelativePath} and '{existing.AegisName}' in {existing.RelativePath}",
                        EntityType = "item",
                        EntityId = item.Id.ToString(),
                        EntityName = item.AegisName,
                        SourceFile = item.SourceFile,
                        Line = item.Line,
                        Recommendation = "Remove or rename one of the duplicates in rAthena DB."
                    });
                }
                else
                {
                    serverItems[item.Id] = item;
                }

                if (string.IsNullOrWhiteSpace(item.AegisName))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "warning",
                        Code = "ITEM_MISSING_AEGIS",
                        Message = $"Server item {item.Id} has no AegisName.",
                        EntityType = "item",
                        EntityId = item.Id.ToString(),
                        SourceFile = item.SourceFile,
                        Line = item.Line,
                        Recommendation = "Add an AegisName to the server item."
                    });
                }
            }
        }

        return issues;
    }

    private static bool IsCrossModeBaseDuplicate(ItemEntry existing, ItemEntry current)
    {
        if (existing.DbMode == "import" || current.DbMode == "import")
        {
            return false;
        }

        if (existing.DbMode == "unknown" || current.DbMode == "unknown")
        {
            return false;
        }

        return !string.Equals(existing.DbMode, current.DbMode, StringComparison.OrdinalIgnoreCase);
    }

    public static List<ValidationIssue> ValidateMonsters(EntityIndex index)
    {
        var issues = new List<ValidationIssue>();
        var seen = new Dictionary<int, MonsterEntry>();

        foreach (var mob in index.Monsters)
        {
            if (seen.TryGetValue(mob.Id, out var existing))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    Code = "MOB_DUPLICATE_ID",
                    Message = $"Duplicate monster ID {mob.Id}: '{mob.AegisName}' and '{existing.AegisName}'",
                    EntityType = "monster",
                    EntityId = mob.Id.ToString(),
                    EntityName = mob.AegisName,
                    SourceFile = mob.SourceFile,
                    Line = mob.Line,
                    Recommendation = "Remove or rename one of the duplicates."
                });
            }
            else
            {
                seen[mob.Id] = mob;
            }

            if (string.IsNullOrWhiteSpace(mob.AegisName) || string.IsNullOrWhiteSpace(mob.Name))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    Code = "MOB_MISSING_NAME",
                    Message = $"Monster {mob.Id} missing AegisName or Name.",
                    EntityType = "monster",
                    EntityId = mob.Id.ToString(),
                    SourceFile = mob.SourceFile,
                    Line = mob.Line,
                    Recommendation = "Add AegisName and Name to the monster."
                });
            }
        }

        return issues;
    }

    public static List<ValidationIssue> ValidateNpcs(EntityIndex index)
    {
        var issues = new List<ValidationIssue>();

        foreach (var npc in index.Npcs)
        {
            if (string.IsNullOrWhiteSpace(npc.Map))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    Code = "NPC_NO_MAP",
                    Message = $"NPC '{npc.Name}' has no map.",
                    EntityType = "npc",
                    EntityName = npc.Name,
                    SourceFile = npc.SourceFile,
                    Line = npc.Line,
                    Recommendation = "Assign a valid map to the NPC."
                });
            }

            if (npc.X < 0 || npc.Y < 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    Code = "NPC_INVALID_COORDS",
                    Message = $"NPC '{npc.Name}' has invalid coordinates ({npc.X},{npc.Y}).",
                    EntityType = "npc",
                    EntityName = npc.Name,
                    SourceFile = npc.SourceFile,
                    Line = npc.Line,
                    Recommendation = "Fix coordinates."
                });
            }
        }

        return issues;
    }

    public static List<ValidationIssue> ValidateMaps(EntityIndex index)
    {
        var issues = new List<ValidationIssue>();

        foreach (var map in index.Maps)
        {
            if (map.Source == "server" && !map.HasRsw && !map.HasGnd && !map.HasGat)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    Code = "MAP_NO_CLIENT_FILES",
                    Message = $"Server map '{map.Name}' has no client files (rsw/gnd/gat).",
                    EntityType = "map",
                    EntityName = map.Name,
                    SourceFile = map.SourceFile,
                    Recommendation = "Add client map files or verify patch path."
                });
            }

            if (map.HasRsw && (!map.HasGnd || !map.HasGat))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    Code = "MAP_INCOMPLETE_CLIENT",
                    Message = $"Map '{map.Name}' has .rsw but missing .gnd or .gat.",
                    EntityType = "map",
                    EntityName = map.Name,
                    Recommendation = "Add missing .gnd/.gat files."
                });
            }
        }

        return issues;
    }
}
