using System.Text;
using System.Text.RegularExpressions;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Infrastructure;

public sealed record StagedTextFile(
    string TargetPath,
    string StagingPath,
    string Content);

public sealed class ApplyPostWriteValidator
{
    private readonly YamlSyntaxValidator _yamlValidator = new();
    private readonly RathenaTxtValidator _txtValidator = new();
    private readonly RathenaScriptValidator _scriptValidator = new();
    private readonly LuaTextValidator _luaValidator = new();
    private readonly LegacyClientIdentityTxtValidator _legacyClientIdentityTxtValidator = new();
    private readonly LegacyItemTxtValidator _legacyItemTxtValidator = new();

    public PostWriteValidationSummary Validate(IReadOnlyList<StagedTextFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var results = files
            .Select(ValidateFile)
            .ToArray();

        return new PostWriteValidationSummary(
            results.All(result => result.IsValid),
            results);
    }

    private PostWriteValidationFileResult ValidateFile(StagedTextFile file)
    {
        var fileName = Path.GetFileName(file.TargetPath);
        if (fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            return _yamlValidator.Validate(file.TargetPath, file.StagingPath, file.Content);
        }

        if (fileName.Equals("mob_skill_db.txt", StringComparison.OrdinalIgnoreCase))
        {
            return _txtValidator.Validate(file.TargetPath, file.StagingPath, file.Content);
        }

        if (fileName.Equals("jobname.txt", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("jobidentity.txt", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("npcidentity.txt", StringComparison.OrdinalIgnoreCase))
        {
            return _legacyClientIdentityTxtValidator.Validate(file.TargetPath, file.StagingPath, file.Content);
        }

        if (LegacyItemTxtValidator.IsLegacyItemTable(fileName))
        {
            return _legacyItemTxtValidator.Validate(file.TargetPath, file.StagingPath, file.Content);
        }

        if (fileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".lub", StringComparison.OrdinalIgnoreCase))
        {
            return _luaValidator.Validate(file.TargetPath, file.StagingPath, file.Content);
        }

        if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".conf", StringComparison.OrdinalIgnoreCase))
        {
            return _scriptValidator.Validate(file.TargetPath, file.StagingPath, file.Content);
        }

        return new PostWriteValidationFileResult(
            file.TargetPath,
            file.StagingPath,
            "ApplyPostWriteValidator",
            true,
            [
                new PostWriteValidationIssue(
                    "validator.skipped",
                    "No post-write validator is registered for this file type in the current milestone.",
                    PostWriteValidationSeverity.Info)
            ]);
    }
}

public sealed class LegacyItemTxtValidator
{
    private static readonly HashSet<string> LegacyItemTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "idnum2itemdisplaynametable.txt",
        "idnum2itemresnametable.txt",
        "idnum2itemdesctable.txt",
        "num2itemdisplaynametable.txt",
        "num2itemresnametable.txt",
        "num2itemdesctable.txt",
        "itemslotcounttable.txt"
    };

    public static bool IsLegacyItemTable(string fileName) => LegacyItemTables.Contains(fileName);

    public PostWriteValidationFileResult Validate(string targetPath, string stagingPath, string content)
    {
        var issues = new List<PostWriteValidationIssue>();
        var fileName = Path.GetFileName(targetPath);
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(content))
        {
            return new PostWriteValidationFileResult(targetPath, stagingPath, "LegacyItemTxtValidator", true, []);
        }

        if (fileName.Contains("desctable", StringComparison.OrdinalIgnoreCase))
        {
            ValidateDescriptionTable(normalized, issues);
        }
        else if (fileName.Equals("itemslotcounttable.txt", StringComparison.OrdinalIgnoreCase))
        {
            ValidateSimpleHashTable(normalized, issues, requireNumericValue: true);
        }
        else
        {
            ValidateSimpleHashTable(normalized, issues, requireNumericValue: false);
        }

        return new PostWriteValidationFileResult(
            targetPath,
            stagingPath,
            "LegacyItemTxtValidator",
            issues.All(issue => issue.Severity != PostWriteValidationSeverity.Error),
            issues);
    }

    private static void ValidateSimpleHashTable(string content, ICollection<PostWriteValidationIssue> issues, bool requireNumericValue)
    {
        var lines = content.Split('\n', StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = trimmed.Split('#', StringSplitOptions.None);
            if (parts.Length < 3 || !int.TryParse(parts[0].Trim(), out _))
            {
                issues.Add(Error("itemtxt.entry", $"Line {index + 1} is not a valid id#value# entry."));
                continue;
            }

            if (requireNumericValue && !int.TryParse(parts[1].Trim(), out _))
            {
                issues.Add(Error("itemtxt.numeric", $"Line {index + 1} should contain a numeric slot count."));
            }
        }
    }

    private static void ValidateDescriptionTable(string content, ICollection<PostWriteValidationIssue> issues)
    {
        if (content.Count(character => character == '#') % 2 != 0)
        {
            issues.Add(Error("itemtxt.desc-terminator", "Description table should keep balanced # delimiters."));
        }

        var idLines = content.Split('\n', StringSplitOptions.None)
            .Select((line, index) => (Line: line.Trim(), Index: index + 1))
            .Where(item => item.Line.EndsWith("#", StringComparison.Ordinal) && item.Line.Length > 1);
        foreach (var item in idLines)
        {
            var id = item.Line.TrimEnd('#').Trim();
            if (!int.TryParse(id, out _))
            {
                issues.Add(Error("itemtxt.desc-id", $"Line {item.Index} starts a description block without a numeric ID."));
            }
        }
    }

    private static PostWriteValidationIssue Error(string code, string message) =>
        new(code, message, PostWriteValidationSeverity.Error);
}

public sealed class LegacyClientIdentityTxtValidator
{
    public PostWriteValidationFileResult Validate(string targetPath, string stagingPath, string content)
    {
        var issues = new List<PostWriteValidationIssue>();
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);
        string? delimiter = null;
        var fileName = Path.GetFileName(targetPath);
        var numericValueExpected = fileName.StartsWith("jobidentity", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("npcidentity", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
        {
            issues.Add(Error("legacytxt.empty", "Legacy TXT staging content is empty."));
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var detectedDelimiter = trimmed.Contains('=') ? "=" : trimmed.Contains(',') ? "," : null;
            if (detectedDelimiter is null)
            {
                issues.Add(Error("legacytxt.delimiter", $"Line {index + 1} does not use a recognized legacy TXT delimiter."));
                continue;
            }

            delimiter ??= detectedDelimiter;
            if (!delimiter.Equals(detectedDelimiter, StringComparison.Ordinal))
            {
                issues.Add(Error("legacytxt.mixed-delimiter", $"Line {index + 1} mixes delimiters; keep the file on a single TXT layout."));
            }

            var parts = trimmed.Split([detectedDelimiter], 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                issues.Add(Error("legacytxt.entry", $"Line {index + 1} is not a valid key/value entry."));
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            if (!Regex.IsMatch(key, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
            {
                issues.Add(Error("legacytxt.key", $"Line {index + 1} uses an unsafe identity key '{key}'."));
            }

            if (value.Length == 0)
            {
                issues.Add(Error("legacytxt.value", $"Line {index + 1} is missing the value part."));
                continue;
            }

            if (numericValueExpected && !int.TryParse(value, out _))
            {
                issues.Add(Error("legacytxt.numeric", $"Line {index + 1} should contain a numeric identity value."));
            }
        }

        return new PostWriteValidationFileResult(
            targetPath,
            stagingPath,
            "LegacyClientIdentityTxtValidator",
            issues.All(issue => issue.Severity != PostWriteValidationSeverity.Error),
            issues);
    }

    private static PostWriteValidationIssue Error(string code, string message) =>
        new(code, message, PostWriteValidationSeverity.Error);
}

public sealed class YamlSyntaxValidator
{
    public PostWriteValidationFileResult Validate(string targetPath, string stagingPath, string content)
    {
        var issues = new List<PostWriteValidationIssue>();
        var lines = Normalize(content).Split('\n', StringSplitOptions.None);
        var previousIndent = 0;
        int? blockScalarParentIndent = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            issues.Add(Error("yaml.empty", "YAML staging content is empty."));
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (rawLine.Contains('\t'))
            {
                issues.Add(Error("yaml.tab-indent", $"Line {index + 1} contains a tab character; generated YAML must stay space-indented."));
            }

            var indent = rawLine.Length - rawLine.TrimStart(' ').Length;
            if (blockScalarParentIndent is not null)
            {
                if (indent > blockScalarParentIndent.Value)
                {
                    previousIndent = indent;
                    continue;
                }

                blockScalarParentIndent = null;
            }

            if (indent % 2 != 0)
            {
                issues.Add(Error("yaml.odd-indent", $"Line {index + 1} uses odd indentation ({indent} spaces)."));
            }

            if (indent > previousIndent + 2)
            {
                issues.Add(Error("yaml.jump-indent", $"Line {index + 1} increases indentation too abruptly from {previousIndent} to {indent}."));
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!trimmed.Contains(":", StringComparison.Ordinal))
                {
                    issues.Add(Error("yaml.list-entry", $"Line {index + 1} looks like a YAML list entry but is missing a key/value separator."));
                }
            }
            else if (!trimmed.EndsWith(":", StringComparison.Ordinal) && !trimmed.Contains(":", StringComparison.Ordinal))
            {
                issues.Add(Error("yaml.mapping", $"Line {index + 1} is not a valid mapping entry."));
            }

            if (trimmed.EndsWith("|", StringComparison.Ordinal) || trimmed.EndsWith(">-", StringComparison.Ordinal) || trimmed.EndsWith(">", StringComparison.Ordinal))
            {
                blockScalarParentIndent = indent;
            }

            previousIndent = indent;
        }

        return BuildResult(targetPath, stagingPath, "YamlSyntaxValidator", issues);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static PostWriteValidationIssue Error(string code, string message) =>
        new(code, message, PostWriteValidationSeverity.Error);

    private static PostWriteValidationFileResult BuildResult(
        string targetPath,
        string stagingPath,
        string validatorName,
        IReadOnlyList<PostWriteValidationIssue> issues) =>
        new(
            targetPath,
            stagingPath,
            validatorName,
            issues.All(issue => issue.Severity != PostWriteValidationSeverity.Error),
            issues);
}

public sealed class RathenaTxtValidator
{
    private const int ExpectedMobSkillColumns = 19;

    public PostWriteValidationFileResult Validate(string targetPath, string stagingPath, string content)
    {
        var issues = new List<PostWriteValidationIssue>();
        var lines = Normalize(content).Split('\n', StringSplitOptions.None);

        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = trimmed.Split(',', StringSplitOptions.None);
            if (columns.Length != ExpectedMobSkillColumns)
            {
                issues.Add(Error("mobskill.column-count", $"Line {index + 1} has {columns.Length} columns; expected {ExpectedMobSkillColumns}."));
                continue;
            }

            ValidateInt(columns[0], index, 1, "MobID", issues);
            if (string.IsNullOrWhiteSpace(columns[1]))
            {
                issues.Add(Error("mobskill.anchor.empty", $"Line {index + 1} is missing the mob skill anchor/dummy column."));
            }

            if (string.IsNullOrWhiteSpace(columns[2]))
            {
                issues.Add(Error("mobskill.state.empty", $"Line {index + 1} is missing the state column."));
            }

            ValidateInt(columns[3], index, 4, "SkillID", issues);
            ValidateInt(columns[4], index, 5, "SkillLv", issues);
            ValidateInt(columns[5], index, 6, "Rate", issues);
            ValidateInt(columns[6], index, 7, "CastTime", issues);
            ValidateInt(columns[7], index, 8, "Delay", issues);

            var cancelable = columns[8].Trim();
            if (!cancelable.Equals("yes", StringComparison.OrdinalIgnoreCase)
                && !cancelable.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("mobskill.cancelable", $"Line {index + 1} uses unsupported Cancelable value '{columns[8]}'."));
            }

            if (string.IsNullOrWhiteSpace(columns[9]))
            {
                issues.Add(Error("mobskill.target.empty", $"Line {index + 1} is missing the target column."));
            }

            if (string.IsNullOrWhiteSpace(columns[10]))
            {
                issues.Add(Error("mobskill.condition.empty", $"Line {index + 1} is missing the condition type column."));
            }

            ValidateOptionalInt(columns[11], index, 12, "Condition value", issues);
            ValidateOptionalInt(columns[12], index, 13, "val1", issues);
            ValidateOptionalInt(columns[13], index, 14, "val2", issues);
            ValidateOptionalInt(columns[14], index, 15, "val3", issues);
            ValidateOptionalInt(columns[15], index, 16, "val4", issues);
            ValidateOptionalInt(columns[16], index, 17, "val5", issues);
            ValidateOptionalInt(columns[17], index, 18, "Emotion", issues);
        }

        return BuildResult(targetPath, stagingPath, "RathenaTxtValidator", issues);
    }

    private static void ValidateInt(string value, int lineIndex, int column, string label, ICollection<PostWriteValidationIssue> issues)
    {
        if (!int.TryParse(value.Trim(), out _))
        {
            issues.Add(Error("mobskill.int", $"Line {lineIndex + 1} column {column} ({label}) is not a valid integer."));
        }
    }

    private static void ValidateOptionalInt(string value, int lineIndex, int column, string label, ICollection<PostWriteValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ValidateInt(value, lineIndex, column, label, issues);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static PostWriteValidationIssue Error(string code, string message) =>
        new(code, message, PostWriteValidationSeverity.Error);

    private static PostWriteValidationFileResult BuildResult(
        string targetPath,
        string stagingPath,
        string validatorName,
        IReadOnlyList<PostWriteValidationIssue> issues) =>
        new(
            targetPath,
            stagingPath,
            validatorName,
            issues.All(issue => issue.Severity != PostWriteValidationSeverity.Error),
            issues);
}

public sealed partial class RathenaScriptValidator
{
    public PostWriteValidationFileResult Validate(string targetPath, string stagingPath, string content)
    {
        var issues = new List<PostWriteValidationIssue>();
        var lines = Normalize(content).Split('\n', StringSplitOptions.None);
        var hasRecognizedLine = false;
        var braceBalance = 0;

        if (string.IsNullOrWhiteSpace(content))
        {
            issues.Add(Error("script.empty", "Script staging content is empty."));
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            braceBalance += rawLine.Count(character => character == '{');
            braceBalance -= rawLine.Count(character => character == '}');

            if (trimmed.StartsWith("npc:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("//npc:", StringComparison.OrdinalIgnoreCase))
            {
                hasRecognizedLine = true;
                continue;
            }

            if (trimmed.Contains("\tmonster\t", StringComparison.Ordinal))
            {
                hasRecognizedLine = true;
                if (!MonsterSpawnLineRegex().IsMatch(trimmed))
                {
                    issues.Add(Error("script.spawn-line", $"Line {index + 1} is not a valid monster spawn line."));
                }

                continue;
            }

            if (trimmed.Contains("\tscript\t", StringComparison.Ordinal) || trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                hasRecognizedLine = true;
                continue;
            }

            if (trimmed.StartsWith("On", StringComparison.Ordinal)
                || trimmed.EndsWith(";", StringComparison.Ordinal)
                || trimmed.Equals("{", StringComparison.Ordinal)
                || trimmed.Equals("}", StringComparison.Ordinal))
            {
                hasRecognizedLine = true;
            }
        }

        if (braceBalance != 0)
        {
            issues.Add(Error("script.braces", $"Script brace balance ended at {braceBalance}; expected 0."));
        }

        if (!hasRecognizedLine)
        {
            issues.Add(Error("script.unrecognized", "The staged text did not contain a recognized rAthena script, spawn or loader line."));
        }

        return BuildResult(targetPath, stagingPath, "RathenaScriptValidator", issues);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static PostWriteValidationIssue Error(string code, string message) =>
        new(code, message, PostWriteValidationSeverity.Error);

    private static PostWriteValidationFileResult BuildResult(
        string targetPath,
        string stagingPath,
        string validatorName,
        IReadOnlyList<PostWriteValidationIssue> issues) =>
        new(
            targetPath,
            stagingPath,
            validatorName,
            issues.All(issue => issue.Severity != PostWriteValidationSeverity.Error),
            issues);

    [GeneratedRegex(@"^[A-Za-z0-9_]+,\d+,\d+,\d+,\d+\tmonster\t[^\t]+\t\d+,\d+,\d+,\d+(?:,[A-Za-z0-9_:@]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex MonsterSpawnLineRegex();
}

public sealed class LuaTextValidator
{
    public PostWriteValidationFileResult Validate(string targetPath, string stagingPath, string content)
    {
        var issues = new List<PostWriteValidationIssue>();
        if (string.IsNullOrWhiteSpace(content))
        {
            issues.Add(Error("lua.empty", "Lua staging content is empty."));
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalized.Count(character => character == '{') != normalized.Count(character => character == '}'))
        {
            issues.Add(Error("lua.braces", "Lua text has unbalanced braces."));
        }

        if (normalized.Count(character => character == '(') != normalized.Count(character => character == ')'))
        {
            issues.Add(Error("lua.parentheses", "Lua text has unbalanced parentheses."));
        }

        if (normalized.Count(character => character == '"') % 2 != 0)
        {
            issues.Add(Error("lua.quotes", "Lua text has an odd number of double quotes."));
        }

        return new PostWriteValidationFileResult(
            targetPath,
            stagingPath,
            "LuaTextValidator",
            issues.All(issue => issue.Severity != PostWriteValidationSeverity.Error),
            issues);
    }

    private static PostWriteValidationIssue Error(string code, string message) =>
        new(code, message, PostWriteValidationSeverity.Error);
}
