using System.Text;
using System.Text.RegularExpressions;
using RagnaForge.Domain.Items;
using RagnaForge.Infrastructure.FileSystem;
using RagnaForge.Infrastructure.Patch;

namespace RagnaForge.Infrastructure.Items;

public sealed partial class ClientSidePlanner
{
    private static readonly string[] LegacyItemTableNames =
    [
        "idnum2itemdisplaynametable.txt",
        "idnum2itemresnametable.txt",
        "idnum2itemdesctable.txt",
        "num2itemdisplaynametable.txt",
        "num2itemresnametable.txt",
        "num2itemdesctable.txt",
        "itemslotcounttable.txt"
    ];

    private readonly LuaScriptFormatDetector _formatDetector = new();

    public ClientSidePlan CreateItemPlan(
        string patchPath,
        int itemId,
        ItemDefinitionInput input,
        IReadOnlyList<string> identifiedDescriptionLines,
        string unidentifiedName,
        string unidentifiedResourceName,
        IReadOnlyList<string> unidentifiedDescriptionLines)
    {
        var itemInfoFiles = DetectItemInfoFiles(patchPath);
        var legacyFiles = DetectLegacyItemTables(patchPath);
        var itemInfoDetected = itemInfoFiles.Any(file => file.Exists);
        var legacyDetected = legacyFiles.Any(file => file.Exists);
        var hybridDetected = itemInfoDetected && legacyDetected;
        var detectedFiles = itemInfoFiles.Concat(legacyFiles).ToArray();
        var blockReasons = new List<string>();
        var warnings = new List<string>();
        var errors = new List<string>();
        var proposedChanges = new List<ProposedFileChange>();
        var proposedRegistrations = new List<string>();
        var existingRegistrations = new List<string>();

        var selectedItemInfo = SelectWritableFile(itemInfoFiles);
        var legacyComplete = LegacyItemTableNames.All(name =>
            legacyFiles.Any(file => file.Exists && Path.GetFileName(file.Path).Equals(name, StringComparison.OrdinalIgnoreCase)));

        var bytecodeFiles = detectedFiles
            .Where(file => file.Format == ClientSideFileFormat.BinaryLub)
            .Select(file => file.Path)
            .ToArray();
        var unsupportedFiles = detectedFiles
            .Where(file => file.Exists && !file.SupportedForApply)
            .Select(file => file.Path)
            .ToArray();

        foreach (var file in legacyFiles.Where(file => file.Exists && FileContainsItemId(file.Path, itemId)))
        {
            existingRegistrations.Add($"{Path.GetFileName(file.Path)} already contains item ID {itemId}.");
        }

        foreach (var file in itemInfoFiles.Where(file => file.Exists && FileContainsItemId(file.Path, itemId)))
        {
            existingRegistrations.Add($"{Path.GetFileName(file.Path)} already contains item ID {itemId}.");
        }

        ClientSideMode mode;
        if (hybridDetected)
        {
            mode = ClientSideMode.Hybrid;
            warnings.Add("Hybrid item client detected: itemInfo and legacy TXT tables coexist.");
            if (bytecodeFiles.Length > 0)
            {
                blockReasons.Add("Hybrid client contains bytecode itemInfo; current milestone will not infer a safe write target.");
            }
            else if (legacyComplete)
            {
                warnings.Add("Hybrid client will target complete legacy TXT tables and validate itemInfo as read-only in this milestone.");
                proposedChanges.AddRange(BuildLegacyItemChanges(
                    patchPath,
                    itemId,
                    input.DisplayName,
                    input.ResourceName,
                    identifiedDescriptionLines,
                    unidentifiedName,
                    unidentifiedResourceName,
                    unidentifiedDescriptionLines,
                    input.Slots));
                proposedRegistrations.Add($"legacy TXT item tables => {itemId} ({input.DisplayName}).");
            }
            else
            {
                blockReasons.Add("Hybrid client is ambiguous because legacy tables are incomplete.");
            }
        }
        else if (legacyDetected)
        {
            mode = ClientSideMode.LegacyTxt;
            if (!legacyComplete)
            {
                blockReasons.Add("Legacy TXT client was detected but required item tables are incomplete.");
            }
            else
            {
                proposedChanges.AddRange(BuildLegacyItemChanges(
                    patchPath,
                    itemId,
                    input.DisplayName,
                    input.ResourceName,
                    identifiedDescriptionLines,
                    unidentifiedName,
                    unidentifiedResourceName,
                    unidentifiedDescriptionLines,
                    input.Slots));
                proposedRegistrations.Add($"legacy TXT item tables => {itemId} ({input.DisplayName}).");
            }
        }
        else if (itemInfoDetected)
        {
            mode = ClientSideMode.ItemInfo;
            if (bytecodeFiles.Length > 0)
            {
                blockReasons.Add("itemInfo exists only as bytecode/binary in the selected client path.");
            }
            else if (selectedItemInfo is null)
            {
                blockReasons.Add("itemInfo was detected, but no textual itemInfo file is safe for apply.");
            }
            else
            {
                var preview = BuildItemInfoAppendBlock(
                    itemId,
                    input.DisplayName,
                    input.ResourceName,
                    identifiedDescriptionLines,
                    unidentifiedName,
                    unidentifiedResourceName,
                    unidentifiedDescriptionLines,
                    input.Slots);
                proposedChanges.Add(new ProposedFileChange(selectedItemInfo.Path, "append", true, preview));
                proposedRegistrations.Add($"{Path.GetFileName(selectedItemInfo.Path)} => item ID {itemId} ({input.DisplayName}).");
            }
        }
        else
        {
            mode = ClientSideMode.Unknown;
            blockReasons.Add("No supported itemInfo or legacy TXT item client files were detected.");
        }

        if (existingRegistrations.Count > 0)
        {
            blockReasons.Add($"Client-side item ID {itemId} already exists.");
        }

        var canApply = blockReasons.Count == 0 && proposedChanges.Count > 0;
        errors.AddRange(blockReasons);
        return BuildPlan(
            required: true,
            canApply: canApply,
            mode: mode,
            detectedFiles: detectedFiles,
            itemInfoDetected: itemInfoDetected,
            legacyDetected: legacyDetected,
            blockReasons: blockReasons,
            warnings: warnings,
            errors: errors,
            changes: proposedChanges,
            proposedRegistrations: proposedRegistrations,
            existingRegistrations: existingRegistrations,
            bytecodeFiles: bytecodeFiles,
            unsupportedFiles: unsupportedFiles);
    }

    public ClientSidePlan CreateVisualPlan(
        string patchPath,
        IReadOnlyList<ProposedFileChange> visualChanges)
    {
        var detectedFiles = visualChanges
            .Select(change => DetectExistingFile(change.TargetPath, GuessLogicalName(change.TargetPath), selected: true))
            .ToArray();
        var blockReasons = new List<string>();
        var warnings = new List<string>();
        var errors = new List<string>();
        var bytecodeFiles = detectedFiles
            .Where(file => file.Format == ClientSideFileFormat.BinaryLub)
            .Select(file => file.Path)
            .ToArray();
        var unsupportedFiles = detectedFiles
            .Where(file => file.Exists && !file.SupportedForApply)
            .Select(file => file.Path)
            .ToArray();

        if (bytecodeFiles.Length > 0)
        {
            blockReasons.Add("Visual datainfo contains .lub bytecode and cannot be edited safely.");
        }

        if (unsupportedFiles.Length > 0)
        {
            blockReasons.Add("Visual datainfo contains unsupported files for this milestone.");
        }

        errors.AddRange(blockReasons);
        return BuildPlan(
            required: visualChanges.Count > 0,
            canApply: blockReasons.Count == 0,
            mode: ClientSideMode.ItemInfo,
            detectedFiles: detectedFiles,
            itemInfoDetected: false,
            legacyDetected: false,
            blockReasons: blockReasons,
            warnings: warnings,
            errors: errors,
            changes: visualChanges,
            proposedRegistrations: visualChanges.Select(change => $"{Path.GetFileName(change.TargetPath)} <= {FirstMeaningfulLine(change.Preview)}").ToArray(),
            existingRegistrations: [],
            bytecodeFiles: bytecodeFiles,
            unsupportedFiles: unsupportedFiles);
    }

    public bool IsSupportedTextTarget(string targetPath)
    {
        var detection = DetectExistingFile(targetPath, GuessLogicalName(targetPath), selected: true);
        return detection.SupportedForApply;
    }

    public IReadOnlyList<ProposedFileChange> BuildLegacyItemChanges(
        string patchPath,
        int id,
        string displayName,
        string resourceName,
        IReadOnlyList<string> identifiedDescriptionLines,
        string unidentifiedName,
        string unidentifiedResourceName,
        IReadOnlyList<string> unidentifiedDescriptionLines,
        int slots)
    {
        var dataPath = SafeFileSystem.Combine(patchPath, "data");
        var changes = new List<ProposedFileChange>
        {
            new(SafeFileSystem.Combine(dataPath, "idnum2itemdisplaynametable.txt"), "append", File.Exists(SafeFileSystem.Combine(dataPath, "idnum2itemdisplaynametable.txt")), $"{id}#{displayName}#"),
            new(SafeFileSystem.Combine(dataPath, "idnum2itemresnametable.txt"), "append", File.Exists(SafeFileSystem.Combine(dataPath, "idnum2itemresnametable.txt")), $"{id}#{resourceName}#"),
            new(SafeFileSystem.Combine(dataPath, "idnum2itemdesctable.txt"), "append", File.Exists(SafeFileSystem.Combine(dataPath, "idnum2itemdesctable.txt")), BuildDescriptionTableEntry(id, identifiedDescriptionLines)),
            new(SafeFileSystem.Combine(dataPath, "num2itemdisplaynametable.txt"), "append", File.Exists(SafeFileSystem.Combine(dataPath, "num2itemdisplaynametable.txt")), $"{id}#{unidentifiedName}#"),
            new(SafeFileSystem.Combine(dataPath, "num2itemresnametable.txt"), "append", File.Exists(SafeFileSystem.Combine(dataPath, "num2itemresnametable.txt")), $"{id}#{unidentifiedResourceName}#"),
            new(SafeFileSystem.Combine(dataPath, "num2itemdesctable.txt"), "append", File.Exists(SafeFileSystem.Combine(dataPath, "num2itemdesctable.txt")), BuildDescriptionTableEntry(id, unidentifiedDescriptionLines))
        };

        if (slots > 0)
        {
            changes.Add(new ProposedFileChange(
                SafeFileSystem.Combine(dataPath, "itemslotcounttable.txt"),
                "append",
                File.Exists(SafeFileSystem.Combine(dataPath, "itemslotcounttable.txt")),
                $"{id}#{slots}#"));
        }

        return changes;
    }

    private IReadOnlyList<ClientSideFileDetection> DetectItemInfoFiles(string patchPath)
    {
        var candidates = new[]
        {
            SafeFileSystem.Combine(patchPath, "System", "ItemInfo.lua"),
            SafeFileSystem.Combine(patchPath, "System", "ItemInfo.lub"),
            SafeFileSystem.Combine(patchPath, "System", "iteminfo.lua"),
            SafeFileSystem.Combine(patchPath, "System", "iteminfo.lub"),
            SafeFileSystem.Combine(patchPath, "system", "iteminfo.lua"),
            SafeFileSystem.Combine(patchPath, "system", "iteminfo.lub"),
            SafeFileSystem.Combine(patchPath, "system", "iteminfo_true.lua"),
            SafeFileSystem.Combine(patchPath, "system", "iteminfo_true.lub")
        };

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => DetectExistingFile(path, "itemInfo", selected: false))
            .Where(file => file.Exists)
            .OrderBy(file => file.Format == ClientSideFileFormat.TextLub ? 0 : file.Format == ClientSideFileFormat.TextLua ? 1 : 2)
            .ThenBy(file => file.Path.Length)
            .ToArray();
    }

    private IReadOnlyList<ClientSideFileDetection> DetectLegacyItemTables(string patchPath)
    {
        return LegacyItemTableNames
            .Select(name => SafeFileSystem.Combine(patchPath, "data", name))
            .Select(path => DetectExistingFile(path, Path.GetFileNameWithoutExtension(path), selected: true))
            .ToArray();
    }

    private ClientSideFileDetection DetectExistingFile(string path, string logicalName, bool selected)
    {
        var exists = File.Exists(path);
        var format = Classify(path, exists);
        return new ClientSideFileDetection(
            logicalName,
            path,
            format,
            exists,
            selected,
            format is ClientSideFileFormat.TextLua or ClientSideFileFormat.TextLub or ClientSideFileFormat.LegacyTxt);
    }

    private ClientSideFileFormat Classify(string path, bool exists)
    {
        if (!exists)
        {
            return ClientSideFileFormat.Missing;
        }

        var extension = Path.GetExtension(path);
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return ClientSideFileFormat.LegacyTxt;
        }

        if (extension.Equals(".lua", StringComparison.OrdinalIgnoreCase))
        {
            var inspection = _formatDetector.Inspect(path);
            return inspection.Format == LuaScriptFormat.Text ? ClientSideFileFormat.TextLua : ClientSideFileFormat.Unknown;
        }

        if (extension.Equals(".lub", StringComparison.OrdinalIgnoreCase))
        {
            var inspection = _formatDetector.Inspect(path);
            return inspection.Format switch
            {
                LuaScriptFormat.Text => ClientSideFileFormat.TextLub,
                LuaScriptFormat.LuaBytecode => ClientSideFileFormat.BinaryLub,
                LuaScriptFormat.BinaryUnknown => ClientSideFileFormat.Unknown,
                _ => ClientSideFileFormat.Missing
            };
        }

        return ClientSideFileFormat.Unknown;
    }

    private static ClientSideFileDetection? SelectWritableFile(IReadOnlyList<ClientSideFileDetection> files) =>
        files.FirstOrDefault(file => file.SupportedForApply);

    private static ClientSidePlan BuildPlan(
        bool required,
        bool canApply,
        ClientSideMode mode,
        IReadOnlyList<ClientSideFileDetection> detectedFiles,
        bool itemInfoDetected,
        bool legacyDetected,
        IReadOnlyList<string> blockReasons,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProposedFileChange> changes,
        IReadOnlyList<string> proposedRegistrations,
        IReadOnlyList<string> existingRegistrations,
        IReadOnlyList<string> bytecodeFiles,
        IReadOnlyList<string> unsupportedFiles) =>
        new(
            required,
            canApply,
            blockReasons,
            mode.ToString(),
            mode,
            detectedFiles,
            detectedFiles.Where(file => file.Exists).Select(file => $"{file.LogicalName}:{file.Format}").ToArray(),
            itemInfoDetected,
            legacyDetected,
            itemInfoDetected && legacyDetected,
            detectedFiles.Where(file => file.SupportedForApply).Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            unsupportedFiles,
            bytecodeFiles,
            proposedRegistrations,
            existingRegistrations,
            changes,
            changes.Select(change => change.Preview).ToArray(),
            changes.Select(change => change.TargetPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            changes.Select(change => change.TargetPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            changes.Select(change => $"{Path.GetFileName(change.TargetPath)} => ApplyPostWriteValidator").ToArray(),
            warnings,
            errors,
            canApply ? ClientApplyReadiness.Ready : ClientApplyReadiness.Blocked);

    private static bool FileContainsItemId(string path, int itemId)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var text = File.ReadAllText(path);
        return Regex.IsMatch(text, $@"(?m)^\s*{itemId}\s*#", RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, $@"\[\s*{itemId}\s*\]", RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, $@"(?m)^\s*{itemId}\s*=", RegexOptions.CultureInvariant);
    }

    private static string BuildDescriptionTableEntry(int id, IReadOnlyList<string> descriptionLines)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{id}#");
        foreach (var line in descriptionLines)
        {
            builder.AppendLine(line);
        }

        builder.Append("#");
        return builder.ToString();
    }

    private static string BuildItemInfoAppendBlock(
        int id,
        string displayName,
        string resourceName,
        IReadOnlyList<string> identifiedDescriptionLines,
        string unidentifiedName,
        string unidentifiedResourceName,
        IReadOnlyList<string> unidentifiedDescriptionLines,
        int slots)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"tbl[{id}] = {{");
        builder.AppendLine($"    unidentifiedDisplayName = \"{EscapeLuaString(unidentifiedName)}\",");
        builder.AppendLine($"    unidentifiedResourceName = \"{EscapeLuaString(unidentifiedResourceName)}\",");
        builder.AppendLine("    unidentifiedDescriptionName = {");
        foreach (var line in unidentifiedDescriptionLines)
        {
            builder.AppendLine($"        \"{EscapeLuaString(line)}\",");
        }

        builder.AppendLine("    },");
        builder.AppendLine($"    identifiedDisplayName = \"{EscapeLuaString(displayName)}\",");
        builder.AppendLine($"    identifiedResourceName = \"{EscapeLuaString(resourceName)}\",");
        builder.AppendLine("    identifiedDescriptionName = {");
        foreach (var line in identifiedDescriptionLines)
        {
            builder.AppendLine($"        \"{EscapeLuaString(line)}\",");
        }

        builder.AppendLine("    },");
        builder.AppendLine($"    slotCount = {Math.Max(0, slots)},");
        builder.AppendLine("    ClassNum = 0");
        builder.Append("}");
        return builder.ToString();
    }

    private static string EscapeLuaString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string GuessLogicalName(string path) =>
        Path.GetFileNameWithoutExtension(path);

    private static string? FirstMeaningfulLine(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
}
