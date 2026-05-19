using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge find item/npc/monster/map --id/--name'.
/// Searches cached entity indices. Read-only.
/// </summary>
public sealed class FindCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _entityType; // item, npc, monster, map
    private readonly int? _id;
    private readonly string? _name;

    public FindCommand(string configDir, string agentRoot, string entityType, int? id, string? name)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _entityType = entityType;
        _id = id;
        _name = name;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("find");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            var index = IndexCommand.LoadCachedIndex(_agentRoot, pathsConfig.ActiveProfile, fingerprint, "entities");

            // Task 9: Try specific index if unified is missing
            if (index is null)
                index = IndexCommand.LoadCachedIndex(_agentRoot, pathsConfig.ActiveProfile, fingerprint, _entityType);

            if (index is null)
            {
                output = JsonOutput.Error("find", $"Entity index for '{_entityType}' not found or obsolete. Run 'ragnaforge index --{_entityType} --json' first.");
                output.NextRequiredAction = "run_index";
                return output;
            }

            object? results = _entityType switch
            {
                "item" => FindItems(index),
                "npc" => FindNpcs(index),
                "monster" => FindMonsters(index),
                "map" => FindMaps(index),
                _ => null
            };

            if (results is null)
            {
                return JsonOutput.Error("find", $"Unknown entity type: {_entityType}");
            }

            var resultCount = results switch
            {
                List<ItemEntry> l => l.Count,
                List<NpcEntry> l => l.Count,
                List<MonsterEntry> l => l.Count,
                List<MapEntry> l => l.Count,
                _ => 0
            };

            output.Summary = $"Found {resultCount} {_entityType}(s) matching query.";
            output.Data = new { entityType = _entityType, query = new { id = _id, name = _name }, resultCount, results };
        }
        catch (Exception ex) { output = JsonOutput.Error("find", ex.Message); }
        return output;
    }

    private List<ItemEntry> FindItems(EntityIndex index)
    {
        if (_id.HasValue)
            return index.Items.Where(i => i.Id == _id.Value).ToList();
        if (!string.IsNullOrWhiteSpace(_name))
            return index.Items.Where(i =>
                i.Name.Contains(_name, StringComparison.OrdinalIgnoreCase) ||
                i.AegisName.Contains(_name, StringComparison.OrdinalIgnoreCase)).ToList();
        return [];
    }

    private List<NpcEntry> FindNpcs(EntityIndex index)
    {
        if (!string.IsNullOrWhiteSpace(_name))
            return index.Npcs.Where(n =>
                n.Name.Contains(_name, StringComparison.OrdinalIgnoreCase)).ToList();
        return [];
    }

    private List<MonsterEntry> FindMonsters(EntityIndex index)
    {
        if (_id.HasValue)
            return index.Monsters.Where(m => m.Id == _id.Value).ToList();
        if (!string.IsNullOrWhiteSpace(_name))
            return index.Monsters.Where(m =>
                m.Name.Contains(_name, StringComparison.OrdinalIgnoreCase) ||
                m.AegisName.Contains(_name, StringComparison.OrdinalIgnoreCase)).ToList();
        return [];
    }

    private List<MapEntry> FindMaps(EntityIndex index)
    {
        if (!string.IsNullOrWhiteSpace(_name))
            return index.Maps.Where(m =>
                m.Name.Contains(_name, StringComparison.OrdinalIgnoreCase)).ToList();
        return [];
    }
}
