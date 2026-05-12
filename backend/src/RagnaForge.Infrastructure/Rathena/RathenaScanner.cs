using System.Text.RegularExpressions;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Discovery;
using RagnaForge.Infrastructure.FileSystem;

namespace RagnaForge.Infrastructure.Rathena;

public sealed partial class RathenaScanner : IRathenaScanner
{
    public RathenaDiscoveryResult Scan(string rathenaPath)
    {
        var warnings = new List<string>();
        if (!SafeFileSystem.DirectoryExists(rathenaPath))
        {
            return new RathenaDiscoveryResult(
                rathenaPath,
                false,
                EpisodeMode.Unknown,
                "rAthena path was not found.",
                false,
                false,
                [],
                new MapServerConfigSnapshot(false, null, null, []),
                0,
                0,
                0,
                new NpcScriptConfigSnapshot(false, [], []),
                ["rAthena path was not found."]);
        }

        var importDatabases = new[]
        {
            "db/import/item_db.yml",
            "db/import/mob_db.yml",
            "db/import/mob_avail.yml",
            "db/import/mob_skill_db.txt"
        }.Select(relative => ScanImportDatabase(rathenaPath, relative)).ToArray();

        var renewal = DetectRenewalMode(rathenaPath);
        var mapConfig = ScanMapServerConfig(rathenaPath);
        var mapCounts = CountMaps(rathenaPath);
        var customNpcScripts = ScanCustomNpcScripts(rathenaPath);

        if (renewal.Mode == EpisodeMode.PreRenewal)
        {
            warnings.Add("Current source flags look non-renewal, but this must be treated as the current episode profile only.");
        }

        if (mapConfig.UseGrf == false)
        {
            warnings.Add("map-server use_grf is disabled; map deployments require map_cache.dat handling.");
        }

        return new RathenaDiscoveryResult(
            rathenaPath,
            true,
            renewal.Mode,
            renewal.Reason,
            Directory.Exists(SafeFileSystem.Combine(rathenaPath, "db", "import")),
            Directory.Exists(SafeFileSystem.Combine(rathenaPath, "npc", "custom")),
            importDatabases,
            mapConfig,
            mapCounts.ActiveMaps,
            mapCounts.CommentedMaps,
            CountNonEmptyLines(SafeFileSystem.Combine(rathenaPath, "db", "map_index.txt")),
            customNpcScripts,
            warnings);
    }

    private static ImportDatabaseStats ScanImportDatabase(string root, string relativePath)
    {
        var fullPath = SafeFileSystem.Combine(root, relativePath.Split('/'));
        if (!File.Exists(fullPath))
        {
            return new ImportDatabaseStats(relativePath, false, 0, 0);
        }

        var file = new FileInfo(fullPath);
        var pattern = relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? TextDatabaseEntryRegex()
            : YamlIdEntryRegex();

        var count = File.ReadLines(fullPath).Count(line => pattern.IsMatch(line));
        return new ImportDatabaseStats(relativePath, true, count, file.Length);
    }

    private static (EpisodeMode Mode, string Reason) DetectRenewalMode(string root)
    {
        var renewalPath = SafeFileSystem.Combine(root, "src", "config", "renewal.hpp");
        var lines = SafeFileSystem.ReadLinesIfExists(renewalPath);
        if (lines.Count == 0)
        {
            return (EpisodeMode.Unknown, "src/config/renewal.hpp was not found.");
        }

        var renewalActive = lines.Any(line => ActiveDefineRegex("RENEWAL").IsMatch(line));
        var prereActive = lines.Any(line => ActiveDefineRegex("PRERE").IsMatch(line));

        return (renewalActive, prereActive) switch
        {
            (true, _) => (EpisodeMode.Renewal, "Active #define RENEWAL was detected."),
            (false, true) => (EpisodeMode.PreRenewal, "Active #define PRERE was detected."),
            _ => (EpisodeMode.PreRenewal, "No active RENEWAL define was detected; treat as current episode profile, not a permanent rule.")
        };
    }

    private static MapServerConfigSnapshot ScanMapServerConfig(string root)
    {
        var path = SafeFileSystem.Combine(root, "conf", "map_athena.conf");
        var lines = SafeFileSystem.ReadLinesIfExists(path);
        if (lines.Count == 0)
        {
            return new MapServerConfigSnapshot(false, null, null, []);
        }

        string? dbPath = null;
        bool? useGrf = null;
        var imports = new List<string>();

        foreach (var line in lines.Select(StripComment).Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            if (line.StartsWith("db_path:", StringComparison.OrdinalIgnoreCase))
            {
                dbPath = line["db_path:".Length..].Trim();
            }
            else if (line.StartsWith("use_grf:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["use_grf:".Length..].Trim();
                useGrf = value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("import:", StringComparison.OrdinalIgnoreCase))
            {
                imports.Add(line["import:".Length..].Trim());
            }
        }

        return new MapServerConfigSnapshot(true, dbPath, useGrf, imports);
    }

    private static (int ActiveMaps, int CommentedMaps) CountMaps(string root)
    {
        var path = SafeFileSystem.Combine(root, "conf", "maps_athena.conf");
        var lines = SafeFileSystem.ReadLinesIfExists(path);
        var active = lines.Count(line => line.TrimStart().StartsWith("map:", StringComparison.OrdinalIgnoreCase));
        var commented = lines.Count(line => line.TrimStart().StartsWith("//map:", StringComparison.OrdinalIgnoreCase) || line.TrimStart().StartsWith("// map:", StringComparison.OrdinalIgnoreCase));
        return (active, commented);
    }

    private static NpcScriptConfigSnapshot ScanCustomNpcScripts(string root)
    {
        var path = SafeFileSystem.Combine(root, "npc", "scripts_custom.conf");
        var lines = SafeFileSystem.ReadLinesIfExists(path);
        if (lines.Count == 0)
        {
            return new NpcScriptConfigSnapshot(false, [], []);
        }

        var active = new List<string>();
        var commented = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("npc:", StringComparison.OrdinalIgnoreCase))
            {
                active.Add(trimmed["npc:".Length..].Trim());
            }
            else if (trimmed.StartsWith("//npc:", StringComparison.OrdinalIgnoreCase))
            {
                commented.Add(trimmed["//npc:".Length..].Trim());
            }
        }

        return new NpcScriptConfigSnapshot(true, active, commented);
    }

    private static int CountNonEmptyLines(string path) =>
        SafeFileSystem.ReadLinesIfExists(path).Count(line => !string.IsNullOrWhiteSpace(line));

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }

    private static Regex ActiveDefineRegex(string defineName) =>
        new(@"^\s*#\s*define\s+" + Regex.Escape(defineName) + @"\b", RegexOptions.Compiled);

    [GeneratedRegex(@"^\s*-\s+Id\s*:", RegexOptions.Compiled)]
    private static partial Regex YamlIdEntryRegex();

    [GeneratedRegex(@"^\s*\d+\s*,", RegexOptions.Compiled)]
    private static partial Regex TextDatabaseEntryRegex();
}

