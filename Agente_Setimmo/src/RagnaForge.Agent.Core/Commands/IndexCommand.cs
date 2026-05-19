using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Parsing;
using RagnaForge.Agent.Core.Security;
using RagnaForge.Agent.Core.Scanning;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge index --entities/--items/--npcs/--monsters/--maps --json'.
/// Read-only indexing of rAthena entities. Writes cache inside agentRoot only.
/// </summary>
public sealed class IndexCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _scope; // entities, items, npcs, monsters, maps

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IndexCommand(string configDir, string agentRoot, string scope)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _scope = scope;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("index");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            var rathenaPath = profile.RathenaPath;
            var patchPath = profile.PatchPath;

            if (string.IsNullOrWhiteSpace(rathenaPath))
                return JsonOutput.Error("index", "rathenaPath is not configured.");

            // Task 6: Validate safety and read access
            var safetyIssues = PathGuard.EnsureProfileIsSafe(profile);
            if (safetyIssues.Count > 0)
            {
                output = JsonOutput.Error("index", "Profile safety validation failed.");
                output.Errors.AddRange(safetyIssues);
                return output;
            }

            var guard = new PathGuard(profile.WritableRoots, profile.ReadOnlyRoots);
            var rathenaAccess = guard.EnsureCanRead(rathenaPath);
            if (!rathenaAccess.IsAllowed)
            {
                output = JsonOutput.Error("index", "Security violation: rathenaPath is not accessible.");
                output.Errors.Add(rathenaAccess.Reason ?? "Read access denied.");
                return output;
            }

            if (!Directory.Exists(rathenaPath))
                return JsonOutput.Error("index", $"rathenaPath does not exist: {rathenaPath}");

            var index = new EntityIndex
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                AgentVersion = RagnaForge.Agent.Core.AgentVersion.Current,
                ActiveProfile = pathsConfig.ActiveProfile,
                ConfigFingerprint = fingerprint,
                SourcePaths = [rathenaPath, patchPath ?? ""]
            };

            // Policy: patchPath is optional but warned if missing
            if (!string.IsNullOrWhiteSpace(patchPath))
            {
                var patchAccess = guard.EnsureCanRead(patchPath);
                if (!patchAccess.IsAllowed)
                {
                    index.Warnings.Add("Security warning: patchPath access is restricted. Client enrichment skipped.");
                    patchPath = null; // Degraded mode
                }
                else if (!Directory.Exists(patchPath))
                {
                    index.Warnings.Add($"Warning: patchPath does not exist: {patchPath}. Client enrichment skipped.");
                    patchPath = null; // Degraded mode
                }
            }

            // Task 5: Use secure enumeration from ProjectScanner
            index.Stats.FilesScanned = ProjectScanner.SafeEnumerateFiles(rathenaPath).Count();
            if (!string.IsNullOrWhiteSpace(patchPath))
                index.Stats.FilesScanned += ProjectScanner.SafeEnumerateFiles(patchPath).Count();

            var sw = Stopwatch.StartNew();

            if (_scope is "entities" or "items")
            {
                index.Items = ItemDbParser.ParseAll(rathenaPath, profile.DbMode);

                // Task 7: Client Item Index
                if (!string.IsNullOrWhiteSpace(patchPath))
                {
                    var clientItems = ClientItemParser.ParseAll(patchPath);
                    index.Items.AddRange(clientItems);
                }

                index.Items = index.Items.OrderBy(i => i.Id).ToList();
                index.Stats.ItemsFound = index.Items.Count;
            }

            if (_scope is "entities" or "monsters")
            {
                index.Monsters = MobDbParser.ParseAll(rathenaPath, profile.DbMode)
                    .OrderBy(m => m.Id).ToList();
                index.Stats.MonstersFound = index.Monsters.Count;
            }

            if (_scope is "entities" or "npcs")
            {
                index.Npcs = NpcScriptParser.ParseAll(rathenaPath)
                    .OrderBy(n => n.Map).ThenBy(n => n.Name).ToList();
                index.Stats.NpcsFound = index.Npcs.Count;
            }

            if (_scope is "entities" or "maps")
            {
                index.Maps = MapIndexParser.ParseServerMaps(rathenaPath);
                if (!string.IsNullOrWhiteSpace(patchPath))
                    MapIndexParser.EnrichWithClientAssets(index.Maps, patchPath);
                index.Maps = [.. index.Maps.OrderBy(m => m.Name)];
                index.Stats.MapsFound = index.Maps.Count;
            }

            sw.Stop();
            index.Stats.DurationMs = sw.ElapsedMilliseconds;

            // Task 8: Files Parsed / Skipped
            var allParsedFiles = index.Items.Select(i => i.SourceFile)
                .Concat(index.Monsters.Select(m => m.SourceFile))
                .Concat(index.Npcs.Select(n => n.SourceFile))
                .Concat(index.Maps.Select(m => m.SourceFile))
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct().ToList();

            index.Stats.FilesParsed = allParsedFiles.Count;
            index.Stats.FilesSkipped = index.Stats.FilesScanned - index.Stats.FilesParsed;

            // Save cache
            var cachePath = Path.Combine(_agentRoot, "cache", "agent",
                _scope == "entities" ? "entities_index.json" : $"{_scope.TrimEnd('s')}_index.json");
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath, JsonSerializer.Serialize(index, JsonOpts));

            output.Summary = $"Index completed — {index.Stats.ItemsFound} items, " +
                $"{index.Stats.MonstersFound} monsters, {index.Stats.NpcsFound} NPCs, " +
                $"{index.Stats.MapsFound} maps indexed.";

            output.Data = new
            {
                scope = _scope,
                itemsFound = index.Stats.ItemsFound,
                monstersFound = index.Stats.MonstersFound,
                npcsFound = index.Stats.NpcsFound,
                mapsFound = index.Stats.MapsFound,
                filesScanned = index.Stats.FilesScanned,
                filesParsed = index.Stats.FilesParsed,
                filesSkipped = index.Stats.FilesSkipped,
                durationMs = index.Stats.DurationMs,
                cachePath = Path.GetRelativePath(_agentRoot, cachePath),
                warningCount = index.Warnings.Count
            };
        }
        catch (Exception ex) { output = JsonOutput.Error("index", ex.Message); }
        return output;
    }

    /// <summary>Load cached entity index. Returns null if missing/obsolete.</summary>
    public static EntityIndex? LoadCachedIndex(string agentRoot, string activeProfile, string fingerprint, string scope = "entities")
    {
        var fileName = scope == "entities" ? "entities_index.json" : $"{scope.TrimEnd('s')}_index.json";
        var path = Path.Combine(agentRoot, "cache", "agent", fileName);

        if (!File.Exists(path)) return null;
        try
        {
            var idx = JsonSerializer.Deserialize<EntityIndex>(File.ReadAllText(path), JsonOpts);
            if (idx is null) return null;
            if (idx.ActiveProfile != activeProfile || idx.ConfigFingerprint != fingerprint) return null;
            return idx;
        }
        catch { return null; }
    }
}
