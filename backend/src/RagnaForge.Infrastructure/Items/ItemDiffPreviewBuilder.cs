using System.Text;
using RagnaForge.Domain.Items;

namespace RagnaForge.Infrastructure.Items;

internal static class ItemDiffPreviewBuilder
{
    private const int ContextTailLineCount = 3;

    public static ItemDiffPreview Build(IReadOnlyList<ProposedFileChange> proposedChanges)
    {
        var entries = proposedChanges
            .Select(BuildEntry)
            .ToArray();

        return new ItemDiffPreview(
            entries.Length,
            entries.Count(entry => !entry.Exists),
            entries.Count(entry => entry.Exists),
            entries);
    }

    private static ItemDiffPreviewEntry BuildEntry(ProposedFileChange change)
    {
        var existingText = change.Exists && File.Exists(change.TargetPath)
            ? File.ReadAllText(change.TargetPath)
            : string.Empty;
        var existingLines = SplitLines(existingText);
        var addedLines = SplitLines(change.Preview);
        var contextLines = existingLines.TakeLast(ContextTailLineCount).ToArray();

        var oldStart = existingLines.Length == 0 ? 0 : Math.Max(1, existingLines.Length - contextLines.Length + 1);
        var oldCount = contextLines.Length;
        var newStart = existingLines.Length == 0 ? 1 : oldStart;
        var newCount = contextLines.Length + addedLines.Length;

        var diff = new StringBuilder();
        diff.AppendLine($"--- {change.TargetPath}");
        diff.AppendLine($"+++ {change.TargetPath}");
        diff.AppendLine($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");

        foreach (var line in contextLines)
        {
            diff.Append(' ').AppendLine(line);
        }

        foreach (var line in addedLines)
        {
            diff.Append('+').AppendLine(line);
        }

        return new ItemDiffPreviewEntry(
            change.TargetPath,
            change.ChangeKind,
            change.Exists,
            existingLines.Length,
            addedLines.Length,
            diff.ToString().TrimEnd());
    }

    private static string[] SplitLines(string value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.None);
}
