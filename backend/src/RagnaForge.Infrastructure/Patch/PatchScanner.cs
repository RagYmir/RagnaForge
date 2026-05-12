using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;
using RagnaForge.Infrastructure.FileSystem;
using System.Text.RegularExpressions;

namespace RagnaForge.Infrastructure.Patch;

public sealed partial class PatchScanner : IPatchScanner
{
    private static readonly string[] LegacyItemTables =
    [
        "idnum2itemdesctable.txt",
        "idnum2itemdisplaynametable.txt",
        "idnum2itemresnametable.txt",
        "num2itemdesctable.txt",
        "num2itemdisplaynametable.txt",
        "num2itemresnametable.txt",
        "itemslotcounttable.txt"
    ];

    private static readonly string[] DatainfoFiles =
    [
        "accessoryid.lub",
        "accname.lub",
        "accname_f.lub",
        "jobidentity.lua",
        "jobidentity.lub",
        "jobname.lua",
        "jobname.lub",
        "jobname_f.lub",
        "npcidentity.lua",
        "npcidentity.lub",
        "spriterobeid.lub",
        "spriterobename.lub",
        "spriterobename_f.lub",
        "weapontable.lub",
        "weapontable_f.lub"
    ];

    private static readonly string[] ItemInfoFiles =
    [
        "iteminfo.lua",
        "iteminfo.lub",
        "iteminfo_true.lub",
        "iteminfo_true.lua"
    ];

    private static readonly string[] AssetExtensions =
    [
        ".spr",
        ".act",
        ".bmp",
        ".png",
        ".rsw",
        ".gnd",
        ".gat",
        ".rsm",
        ".rsm2",
        ".str",
        ".lua",
        ".lub"
    ];

    public PatchDiscoveryResult Scan(string patchPath)
    {
        if (!SafeFileSystem.DirectoryExists(patchPath))
        {
            return new PatchDiscoveryResult(
                patchPath,
                false,
                [],
                [],
                new ClientItemDataModeSnapshot(false, false, []),
                [],
                [],
                [],
                new ClientDateDetection(null, "patch-not-found", false),
                [],
                ["Patch/client path was not found."]);
        }

        var warnings = new List<string>();
        var dataIniEntries = ScanDataIni(patchPath);
        if (dataIniEntries.Any(entry => !entry.ExistsOnDisk))
        {
            warnings.Add("DATA.INI references entries that were not found as top-level files or directories.");
        }

        var legacyTables = LegacyItemTables
            .Select(name => SafeFileSystem.Snapshot(SafeFileSystem.Combine(patchPath, "data", name)))
            .ToArray();

        var itemInfoFiles = ItemInfoFiles
            .Select(name => SafeFileSystem.Snapshot(SafeFileSystem.Combine(patchPath, "system", name)))
            .Where(file => file.Exists)
            .ToArray();

        if (legacyTables.Any(table => table.Exists))
        {
            warnings.Add("Legacy item TXT tables were detected; do not assume ItemInfo.lua is the source of truth.");
        }

        if (legacyTables.Any(table => table.Exists) && itemInfoFiles.Length > 0)
        {
            warnings.Add("Legacy item tables and System iteminfo files were both detected; prefer profile-aware client resolution.");
        }

        var datainfo = DatainfoFiles
            .Select(name => SafeFileSystem.Snapshot(SafeFileSystem.Combine(patchPath, "data", "luafiles514", "lua files", "datainfo", name)))
            .ToArray();

        var topLevelContainers = Directory.EnumerateFiles(patchPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => IsContainerExtension(Path.GetExtension(path)))
            .Select(SafeFileSystem.Snapshot)
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var clientExecutables = Directory.EnumerateFiles(patchPath, "*.exe", SearchOption.TopDirectoryOnly)
            .Select(SafeFileSystem.Snapshot)
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var clientDate = DetectClientDate(clientExecutables);

        var assetCounts = CountAssets(SafeFileSystem.Combine(patchPath, "data"));

        return new PatchDiscoveryResult(
            patchPath,
            true,
            dataIniEntries,
            legacyTables,
            new ClientItemDataModeSnapshot(
                legacyTables.Any(table => table.Exists),
                itemInfoFiles.Length > 0,
                itemInfoFiles),
            datainfo,
            topLevelContainers,
            clientExecutables,
            clientDate,
            assetCounts,
            warnings);
    }

    private static IReadOnlyList<DataIniEntry> ScanDataIni(string patchPath)
    {
        var path = SafeFileSystem.Combine(patchPath, "DATA.INI");
        var lines = SafeFileSystem.ReadLinesIfExists(path);
        var entries = new List<DataIniEntry>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = trimmed.Split('=', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var priority))
            {
                continue;
            }

            var value = parts[1].Trim();
            var exists = File.Exists(SafeFileSystem.Combine(patchPath, value)) || Directory.Exists(SafeFileSystem.Combine(patchPath, value));
            entries.Add(new DataIniEntry(priority, value, exists));
        }

        return entries.OrderBy(entry => entry.Priority).ToArray();
    }

    private static IReadOnlyList<AssetExtensionCount> CountAssets(string dataPath)
    {
        if (!Directory.Exists(dataPath))
        {
            return [];
        }

        return AssetExtensions
            .Select(extension => new AssetExtensionCount(
                extension,
                SafeFileSystem.EnumerateFiles(dataPath, "*" + extension, SearchOption.AllDirectories).Count()))
            .ToArray();
    }

    private static bool IsContainerExtension(string extension) =>
        extension.Equals(".grf", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".gpf", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".thor", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".rgz", StringComparison.OrdinalIgnoreCase);

    private static ClientDateDetection DetectClientDate(IReadOnlyList<FileSnapshot> clientExecutables)
    {
        var matches = clientExecutables
            .Select(file => new
            {
                File = file,
                Match = ClientDateRegex().Match(file.Name)
            })
            .Where(item => item.Match.Success)
            .Select(item => new
            {
                item.File,
                Value = $"{item.Match.Groups[1].Value}-{item.Match.Groups[2].Value}-{item.Match.Groups[3].Value}"
            })
            .ToArray();

        if (matches.Length > 0)
        {
            var preferred = matches
                .OrderByDescending(item => item.File.Name.Contains("ragexe", StringComparison.OrdinalIgnoreCase))
                .ThenBy(item => item.File.Name, StringComparer.OrdinalIgnoreCase)
                .First();

            return new ClientDateDetection(
                preferred.Value,
                preferred.File.Name,
                true);
        }

        var fallback = clientExecutables
            .Where(file => file.LastWriteTimeUtc is not null)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        if (fallback is not null && fallback.LastWriteTimeUtc is not null)
        {
            return new ClientDateDetection(
                fallback.LastWriteTimeUtc.Value.UtcDateTime.ToString("yyyy-MM-dd"),
                fallback.Name + ":last-write-time",
                false);
        }

        return new ClientDateDetection(null, "not-detected", false);
    }

    [GeneratedRegex(@"(?<!\d)(20\d{2})[-_]?([01]\d)[-_]?([0-3]\d)(?!\d)", RegexOptions.Compiled)]
    private static partial Regex ClientDateRegex();
}
