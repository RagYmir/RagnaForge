using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge dry-run item/npc/monster/map --input file.json --json'.
/// Plans changes without applying. Saves operation manifest inside agentRoot.
/// </summary>
public sealed class DryRunCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _entityType;
    private readonly string _inputPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DryRunCommand(string configDir, string agentRoot, string entityType, string inputPath)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _entityType = entityType;
        _inputPath = inputPath;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("dry-run");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            // Task 4: Restrict input to agentRoot (includes inputs/dry-run)
            var normalizedInput = PathGuard.Normalize(_inputPath);
            var normalizedAgentRoot = PathGuard.Normalize(_agentRoot);
            if (!PathGuard.IsContainedIn(normalizedInput, normalizedAgentRoot))
            {
                return JsonOutput.Error("dry-run", "Security violation: Input file must be located within the agentRoot directory.");
            }

            var guard = new PathGuard(profile.WritableRoots, profile.ReadOnlyRoots);
            var inputAccess = guard.EnsureCanRead(_inputPath);
            if (!inputAccess.IsAllowed)
            {
                output = JsonOutput.Error("dry-run", "Security violation: Input file path is not allowed.");
                output.Errors.Add(inputAccess.Reason ?? "Read access denied.");
                return output;
            }

            if (!File.Exists(_inputPath))
                return JsonOutput.Error("dry-run", $"Input file not found: {_inputPath}");

            var inputJson = File.ReadAllText(_inputPath);
            object? input;
            try { input = JsonSerializer.Deserialize<JsonElement>(inputJson); }
            catch { return JsonOutput.Error("dry-run", "Invalid JSON in input file."); }

            // Load index for conflict detection
            var index = IndexCommand.LoadCachedIndex(_agentRoot, pathsConfig.ActiveProfile, fingerprint);
            if (index is null)
            {
                output = JsonOutput.Error("dry-run",
                    "Entity index not found or obsolete. Run 'ragnaforge index --entities --json' first.");
                output.NextRequiredAction = "run_index";
                return output;
            }

            var manifest = new OperationManifest
            {
                OperationId = JsonOutput.GenerateOperationId(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ActiveProfile = pathsConfig.ActiveProfile,
                ConfigFingerprint = fingerprint,
                EntityType = _entityType,
                Input = input
            };

            // Plan based on entity type
            switch (_entityType)
            {
                case "item":
                    PlanItem(manifest, (JsonElement)input, index, profile);
                    break;
                case "npc":
                    PlanNpc(manifest, (JsonElement)input, index, profile);
                    break;
                case "monster":
                    PlanMonster(manifest, (JsonElement)input, index, profile);
                    break;
                case "map":
                    PlanMap(manifest, (JsonElement)input, profile);
                    break;
                default:
                    return JsonOutput.Error("dry-run", $"Unknown entity type: {_entityType}");
            }

            // Task 5: Sanitize Affected Files
            foreach (var f in manifest.AffectedFiles)
            {
                var pathErrors = PlannedPathValidator.Validate(f.Path, profile);
                if (pathErrors.Count > 0)
                {
                    manifest.Errors.AddRange(pathErrors);
                }
            }

            // Save manifest
            var opsDir = Path.Combine(_agentRoot, "logs", "operations");
            Directory.CreateDirectory(opsDir);
            var manifestPath = Path.Combine(opsDir, $"{manifest.OperationId}.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));

            // Save diff
            if (manifest.AffectedFiles.Count > 0)
            {
                var diffDir = Path.Combine(_agentRoot, "logs", "diffs");
                Directory.CreateDirectory(diffDir);
                var diffPath = Path.Combine(diffDir, $"{manifest.OperationId}.diff.json");
                File.WriteAllText(diffPath, JsonSerializer.Serialize(
                    new { operationId = manifest.OperationId, files = manifest.AffectedFiles }, JsonOpts));
                manifest.DiffPath = Path.GetRelativePath(_agentRoot, diffPath);
            }

            // Save rollback plan
            var rbDir = Path.Combine(_agentRoot, "logs", "rollbacks");
            Directory.CreateDirectory(rbDir);
            var rbPath = Path.Combine(rbDir, $"{manifest.OperationId}.rollback.json");
            File.WriteAllText(rbPath, JsonSerializer.Serialize(new
            {
                operationId = manifest.OperationId,
                entityType = _entityType,
                applied = false,
                note = "Rollback is informational because real apply is not enabled.",
                affectedFiles = manifest.AffectedFiles.Select(f => new { f.Path, f.Action, f.Description })
            }, JsonOpts));
            manifest.RollbackPlanPath = Path.GetRelativePath(_agentRoot, rbPath);

            manifest.Status = manifest.Errors.Count > 0 ? "blocked" : "planned";
            // Re-save with paths
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));

            output.OperationId = manifest.OperationId;
            output.Summary = manifest.Errors.Count > 0
                ? $"Dry-run blocked — {manifest.Errors.Count} error(s)."
                : $"Dry-run planned — {manifest.AffectedFiles.Count} file(s) would be affected.";

            if (manifest.Errors.Count > 0)
            {
                output.Ok = false;
                output.Errors.AddRange(manifest.Errors);
                output.SafeForAutomation = false;
                output.NextRequiredAction = "fix_errors";
            }

            output.Data = new
            {
                manifest.OperationId,
                manifest.EntityType,
                manifest.Status,
                affectedFiles = manifest.AffectedFiles.Count,
                validationIssues = manifest.ValidationIssues.Count,
                manifestPath = Path.GetRelativePath(_agentRoot, manifestPath),
                diffPath = manifest.DiffPath,
                rollbackPlanPath = manifest.RollbackPlanPath,
                applied = false
            };
        }
        catch (Exception ex) { output = JsonOutput.Error("dry-run", ex.Message); }
        return output;
    }

    private void PlanItem(OperationManifest m, JsonElement input, EntityIndex index, ProfileConfig profile)
    {
        var id = input.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
        var aegis = input.TryGetProperty("aegisName", out var aeProp) ? aeProp.GetString() ?? "" : "";
        var target = input.TryGetProperty("target", out var tProp) ? tProp.GetString() ?? "server" : "server";

        if (id <= 0) { m.Errors.Add("Item id must be > 0."); return; }
        if (string.IsNullOrWhiteSpace(aegis)) { m.Errors.Add("aegisName is required."); return; }

        // Server/client may legitimately share IDs. Only existing server-side items
        // block a server-side item dry-run, and .lub sentinels (Id -1) never count.
        if (target.Contains("server", StringComparison.OrdinalIgnoreCase) &&
            index.Items.Any(i => i.Id == id && i.Id != -1 && i.Side == "server"))
        {
            m.Errors.Add($"Duplicate server item ID {id}: Item already exists server-side.");
        }

        if (target.Contains("server"))
        {
            var dbFile = Path.Combine(profile.RathenaPath, "db", "import", "item_db.yml");
            var itemName = input.TryGetProperty("name", out var n) ? (n.GetString() ?? aegis) : aegis;
            var itemType = input.TryGetProperty("type", out var t) ? (t.GetString() ?? "Etc") : "Etc";
            m.AffectedFiles.Add(new AffectedFile
            {
                Path = dbFile, Action = "append",
                Description = $"Append item {id} ({aegis}) to import item_db.",
                DiffPreview = $"+  - Id: {id}\n+    AegisName: {aegis}\n+    Name: {itemName}\n+    Type: {itemType}"
            });
        }
    }

    private void PlanNpc(OperationManifest m, JsonElement input, EntityIndex index, ProfileConfig profile)
    {
        var name = input.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
        var map = input.TryGetProperty("map", out var mProp) ? mProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(name)) { m.Errors.Add("NPC name is required."); return; }
        if (name.Contains("..") || name.Contains("/") || name.Contains("\\"))
            { m.Errors.Add("NPC name contains illegal characters (.., /, \\)."); return; }
        if (string.IsNullOrWhiteSpace(map)) { m.Errors.Add("NPC map is required."); return; }

        var scriptFile = Path.Combine(profile.RathenaPath, "npc", "custom", $"{name.Replace(" ", "_")}.txt");
        var npcX = input.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 0;
        var npcY = input.TryGetProperty("y", out var yProp) ? yProp.GetInt32() : 0;
        var npcDir = input.TryGetProperty("direction", out var dProp) ? dProp.GetInt32() : 0;
        m.AffectedFiles.Add(new AffectedFile
        {
            Path = scriptFile, Action = "create",
            Description = $"Create NPC script '{name}' on map {map}.",
            DiffPreview = $"+ {map},{npcX},{npcY},{npcDir}\tscript\t{name}\tFAKE_NPC,{{"
        });
    }

    private void PlanMonster(OperationManifest m, JsonElement input, EntityIndex index, ProfileConfig profile)
    {
        var id = input.TryGetProperty("id", out var idP) ? idP.GetInt32() : 0;
        var aegis = input.TryGetProperty("aegisName", out var aeP) ? aeP.GetString() ?? "" : "";

        if (id <= 0) { m.Errors.Add("Monster id must be > 0."); return; }
        if (index.Monsters.Any(mob => mob.Id == id))
            m.Errors.Add($"Monster ID {id} already exists in index.");

        var dbFile = Path.Combine(profile.RathenaPath, "db", "import", "mob_db.yml");
        m.AffectedFiles.Add(new AffectedFile
        {
            Path = dbFile, Action = "append",
            Description = $"Append monster {id} ({aegis}) to import mob_db.",
            DiffPreview = $"+  - Id: {id}\n+    AegisName: {aegis}\n+    Name: {(input.TryGetProperty("name", out var n) ? (n.GetString() ?? aegis) : aegis)}"
        });
    }

    private void PlanMap(OperationManifest m, JsonElement input, ProfileConfig profile)
    {
        var name = input.TryGetProperty("name", out var nP) ? nP.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(name)) { m.Errors.Add("Map name is required."); return; }
        if (name.Contains("..") || name.Contains("/") || name.Contains("\\"))
            { m.Errors.Add("Map name contains illegal characters (.., /, \\)."); return; }

        m.AffectedFiles.Add(new AffectedFile
        {
            Path = Path.Combine(profile.RathenaPath, "db", "map_index.txt"),
            Action = "append",
            Description = $"Append map '{name}' to map_index.txt.",
            DiffPreview = $"+ {name}"
        });
    }
}
