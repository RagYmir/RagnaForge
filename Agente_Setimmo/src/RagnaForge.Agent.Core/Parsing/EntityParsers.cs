using System.Text.RegularExpressions;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Scanning;

namespace RagnaForge.Agent.Core.Parsing;

/// <summary>
/// Lightweight parser for rAthena item_db YAML files.
/// Regex-based — no external YAML library needed.
/// Read-only: never modifies source files.
/// </summary>
public static partial class ItemDbParser
{
    /// <summary>
    /// Parse items from a single YAML file. Conservative parsing.
    /// </summary>
    public static List<ItemEntry> Parse(string filePath, string basePath, string dbMode = "unknown")
    {
        var items = new List<ItemEntry>();
        if (!File.Exists(filePath)) return items;

        var lines = File.ReadAllLines(filePath);
        var relativePath = Path.GetRelativePath(basePath, filePath);
        ItemEntry? current = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // New entry: "  - Id: 501"
            if (trimmed.StartsWith("- Id:"))
            {
                if (current != null) items.Add(current);
                var idStr = trimmed["- Id:".Length..].Trim();
                current = new ItemEntry
                {
                    SourceFile = filePath,
                    RelativePath = relativePath,
                    DbMode = dbMode,
                    Line = i + 1
                };
                if (int.TryParse(idStr, out var id)) current.Id = id;
                else current.Warnings.Add($"Could not parse Id at line {i + 1}");
                continue;
            }

            if (current == null) continue;

            if (TryExtract(trimmed, "AegisName:", out var aegis))
                current.AegisName = aegis;
            else if (TryExtract(trimmed, "Name:", out var name))
                current.Name = name;
            else if (TryExtract(trimmed, "Type:", out var type))
                current.Type = type;
        }

        if (current != null) items.Add(current);
        return items;
    }

    /// <summary>
    /// Parse all item_db files from rAthena db directories.
    /// </summary>
    public static List<ItemEntry> ParseAll(string rathenaPath, string dbMode = "renewal")
    {
        var items = new List<ItemEntry>();
        var patterns = GetServerDbDirectories(dbMode);

        foreach (var (sub, mode) in patterns)
        {
            var dir = Path.Combine(rathenaPath, sub);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "item_db*.yml"))
            {
                items.AddRange(Parse(file, rathenaPath, mode));
            }
        }

        return items;
    }

    private static (string Path, string Mode)[] GetServerDbDirectories(string dbMode)
    {
        return NormalizeDbMode(dbMode) switch
        {
            "pre-renewal" => [("db/pre-re", "pre-renewal"), ("db/import", "import")],
            "hybrid" => [("db/re", "renewal"), ("db/pre-re", "pre-renewal"), ("db/import", "import")],
            _ => [("db/re", "renewal"), ("db/import", "import")]
        };
    }

    private static string NormalizeDbMode(string dbMode)
    {
        var value = dbMode.Trim().ToLowerInvariant();
        return value switch
        {
            "pre" or "pre_re" or "pre-re" or "prere" or "pre-renewal" => "pre-renewal",
            "both" or "hybrid" or "progressive" => "hybrid",
            _ => "renewal"
        };
    }

    private static bool TryExtract(string line, string prefix, out string value)
    {
        value = string.Empty;
        if (!line.StartsWith(prefix)) return false;
        value = line[prefix.Length..].Trim().Trim('"');
        return true;
    }
}

/// <summary>
/// Lightweight parser for rAthena mob_db YAML files.
/// </summary>
public static class MobDbParser
{
    public static List<MonsterEntry> Parse(string filePath, string basePath, string dbMode = "unknown")
    {
        var monsters = new List<MonsterEntry>();
        if (!File.Exists(filePath)) return monsters;

        var lines = File.ReadAllLines(filePath);
        var relativePath = Path.GetRelativePath(basePath, filePath);
        MonsterEntry? current = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();

            if (trimmed.StartsWith("- Id:"))
            {
                if (current != null) monsters.Add(current);
                var idStr = trimmed["- Id:".Length..].Trim();
                current = new MonsterEntry
                {
                    SourceFile = filePath,
                    RelativePath = relativePath,
                    DbMode = dbMode,
                    Line = i + 1
                };
                if (int.TryParse(idStr, out var id)) current.Id = id;
                continue;
            }

            if (current == null) continue;

            if (TryExtract(trimmed, "AegisName:", out var aegis))
                current.AegisName = aegis;
            else if (TryExtract(trimmed, "Name:", out var name))
                current.Name = name;
        }

        if (current != null) monsters.Add(current);
        return monsters;
    }

    public static List<MonsterEntry> ParseAll(string rathenaPath, string dbMode = "renewal")
    {
        var monsters = new List<MonsterEntry>();
        var patterns = GetServerDbDirectories(dbMode);

        foreach (var (sub, mode) in patterns)
        {
            var dir = Path.Combine(rathenaPath, sub);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "mob_db*.yml"))
            {
                monsters.AddRange(Parse(file, rathenaPath, mode));
            }
        }

        return monsters;
    }

    private static (string Path, string Mode)[] GetServerDbDirectories(string dbMode)
    {
        return NormalizeDbMode(dbMode) switch
        {
            "pre-renewal" => [("db/pre-re", "pre-renewal"), ("db/import", "import")],
            "hybrid" => [("db/re", "renewal"), ("db/pre-re", "pre-renewal"), ("db/import", "import")],
            _ => [("db/re", "renewal"), ("db/import", "import")]
        };
    }

    private static string NormalizeDbMode(string dbMode)
    {
        var value = dbMode.Trim().ToLowerInvariant();
        return value switch
        {
            "pre" or "pre_re" or "pre-re" or "prere" or "pre-renewal" => "pre-renewal",
            "both" or "hybrid" or "progressive" => "hybrid",
            _ => "renewal"
        };
    }

    private static bool TryExtract(string line, string prefix, out string value)
    {
        value = string.Empty;
        if (!line.StartsWith(prefix)) return false;
        value = line[prefix.Length..].Trim().Trim('"');
        return true;
    }
}

/// <summary>
/// Parser for rAthena NPC script files (.txt).
/// Detects script, shop and warp patterns.
/// </summary>
public static partial class NpcScriptParser
{
    // Pattern: map,x,y,dir\tscript\tName\tsprite,{
    // Pattern: map,x,y,dir\tshop\tName\tsprite,item:price,...
    // Pattern: map,x,y,dir\twarp\tName\tsprite,destmap,destx,desty
    [GeneratedRegex(@"^(\w[\w_@]*),(\d+),(\d+),(\d+)\t(script|shop|cashshop|warp)\t([^\t]+)\t(\S+)")]
    private static partial Regex NpcLineRegex();

    public static List<NpcEntry> Parse(string filePath, string basePath)
    {
        var npcs = new List<NpcEntry>();
        if (!File.Exists(filePath)) return npcs;

        var lines = File.ReadAllLines(filePath);
        var relativePath = Path.GetRelativePath(basePath, filePath);
        var regex = NpcLineRegex();

        for (var i = 0; i < lines.Length; i++)
        {
            var match = regex.Match(lines[i]);
            if (!match.Success) continue;

            npcs.Add(new NpcEntry
            {
                Map = match.Groups[1].Value,
                X = int.TryParse(match.Groups[2].Value, out var x) ? x : 0,
                Y = int.TryParse(match.Groups[3].Value, out var y) ? y : 0,
                Direction = int.TryParse(match.Groups[4].Value, out var d) ? d : 0,
                Type = match.Groups[5].Value,
                Name = match.Groups[6].Value.Trim(),
                Sprite = match.Groups[7].Value.Split(',')[0],
                SourceFile = filePath,
                RelativePath = relativePath,
                Line = i + 1
            });
        }

        return npcs;
    }

    public static List<NpcEntry> ParseAll(string rathenaPath)
    {
        var npcs = new List<NpcEntry>();
        var npcDir = Path.Combine(rathenaPath, "npc");
        if (!Directory.Exists(npcDir)) return npcs;

        foreach (var file in ProjectScanner.SafeEnumerateFiles(npcDir)
            .Where(f => Path.GetExtension(f).Equals(".txt", StringComparison.OrdinalIgnoreCase)))
        {
            npcs.AddRange(Parse(file, rathenaPath));
        }

        return npcs;
    }
}

/// <summary>
/// Parser for rAthena map_index.txt and client map assets.
/// </summary>
public static class MapIndexParser
{
    public static List<MapEntry> ParseServerMaps(string rathenaPath)
    {
        var maps = new List<MapEntry>();
        var indexFile = Path.Combine(rathenaPath, "db", "map_index.txt");
        if (!File.Exists(indexFile)) return maps;

        var lines = File.ReadAllLines(indexFile);
        var relativePath = Path.GetRelativePath(rathenaPath, indexFile);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

            var parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var mapName = parts[0];
            if (!char.IsLetter(mapName[0]) && mapName[0] != '_') continue;

            maps.Add(new MapEntry
            {
                Name = mapName,
                Source = "server",
                SourceFile = indexFile,
                RelativePath = relativePath,
                Confidence = "high"
            });
        }

        return maps;
    }

    /// <summary>
    /// Detect client map assets (.rsw, .gnd, .gat) and consolidate.
    /// </summary>
    public static void EnrichWithClientAssets(List<MapEntry> maps, string patchPath)
    {
        if (!Directory.Exists(patchPath)) return;

        var rswFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gndFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gatFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var f in ProjectScanner.SafeEnumerateFiles(patchPath))
            {
                var ext = Path.GetExtension(f);
                if (ext.Equals(".rsw", StringComparison.OrdinalIgnoreCase))
                    rswFiles.Add(Path.GetFileNameWithoutExtension(f));
                else if (ext.Equals(".gnd", StringComparison.OrdinalIgnoreCase))
                    gndFiles.Add(Path.GetFileNameWithoutExtension(f));
                else if (ext.Equals(".gat", StringComparison.OrdinalIgnoreCase))
                    gatFiles.Add(Path.GetFileNameWithoutExtension(f));
            }
        }
        catch { /* Access denied or similar — skip client enrichment */ }

        foreach (var map in maps)
        {
            map.HasRsw = rswFiles.Contains(map.Name);
            map.HasGnd = gndFiles.Contains(map.Name);
            map.HasGat = gatFiles.Contains(map.Name);

            if (map.HasRsw || map.HasGnd || map.HasGat)
                map.Source = "both";
        }
    }
}

/// <summary>
/// Parser for client-side item data (Lua, Lub, Txt).
/// Blocks editing of .lub files.
/// </summary>
public static class ClientItemParser
{
    public static List<ItemEntry> ParseAll(string patchPath)
    {
        var items = new List<ItemEntry>();
        if (!Directory.Exists(patchPath)) return items;

        // 1. Detect .lub files (ReadOnly Policy)
        var safeFiles = ProjectScanner.SafeEnumerateFiles(patchPath).ToList();
        var lubFiles = safeFiles.Where(f => Path.GetFileName(f).Equals("iteminfo.lub", StringComparison.OrdinalIgnoreCase));
        foreach (var f in lubFiles)
        {
            items.Add(new ItemEntry
            {
                Id = -1,
                AegisName = "LUB_FILE",
                Name = Path.GetFileName(f),
                Side = "client",
                SourceFile = f,
                Confidence = "low",
                Warnings = ["LUB file detected and treated as read-only bytecode."]
            });
        }

        // 2. Parse TXT tables
        var txtPatterns = new[] {
            "idnum2itemdisplaynametable.txt", "idnum2itemdesctable.txt", "idnum2itemresnametable.txt",
            "num2itemdisplaynametable.txt", "num2itemdesctable.txt", "num2itemresnametable.txt"
        };

        foreach (var pattern in txtPatterns)
        {
            foreach (var f in safeFiles.Where(f => Path.GetFileName(f).Equals(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                items.AddRange(ParseTxt(f, patchPath));
            }
        }

        return items;
    }

    private static List<ItemEntry> ParseTxt(string filePath, string basePath)
    {
        var entries = new List<ItemEntry>();
        if (!File.Exists(filePath)) return entries;

        try
        {
            var lines = File.ReadAllLines(filePath);
            var relPath = Path.GetRelativePath(basePath, filePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

                // Format: ID#Name#
                var parts = line.Split('#');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var id))
                {
                    entries.Add(new ItemEntry
                    {
                        Id = id,
                        Name = parts[1].Trim(),
                        Side = "client",
                        SourceFile = filePath,
                        RelativePath = relPath,
                        Confidence = "medium"
                    });
                }
            }
        }
        catch { /* Ignore read errors during indexing */ }

        return entries;
    }
}
