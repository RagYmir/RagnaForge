using System.Collections;
using System.Reflection;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;

namespace RagnaForge.Infrastructure.GrfEditorIntegration;

public sealed class GrfAssemblyContainerInspector : IGrfContainerInspector
{
    public GrfContainerInspectionResult Inspect(string grfEditorPath, string containerPath, int maxEntries)
    {
        if (string.IsNullOrWhiteSpace(grfEditorPath))
        {
            throw new InvalidOperationException("GRF Editor path is required.");
        }

        if (string.IsNullOrWhiteSpace(containerPath))
        {
            throw new InvalidOperationException("Container path is required.");
        }

        var hostExecutablePath = Path.Combine(grfEditorPath, "GrfCL.exe");
        if (!File.Exists(hostExecutablePath))
        {
            throw new InvalidOperationException($"GrfCL.exe was not found in {grfEditorPath}.");
        }

        var fullContainerPath = Path.GetFullPath(containerPath);
        if (!File.Exists(fullContainerPath))
        {
            throw new InvalidOperationException($"Container path was not found: {fullContainerPath}");
        }

        maxEntries = Math.Max(1, maxEntries);
        var warnings = new List<string>();
        var file = new FileInfo(fullContainerPath);

        var loadContext = new GrfEditorAssemblyLoadContext(hostExecutablePath);
        object? holder = null;
        MethodInfo? closeMethod = null;

        try
        {
            var grfAssembly = loadContext.LoadAssembly("GRF");
            var holderType = grfAssembly.GetType("GRF.Core.GrfHolder", throwOnError: true)!;
            holder = Activator.CreateInstance(holderType)
                     ?? throw new InvalidOperationException("Could not instantiate GRF.Core.GrfHolder.");

            var openMethod = holderType.GetMethod("Open", [typeof(string)])
                             ?? throw new InvalidOperationException("GRF.Core.GrfHolder.Open(string) was not found.");
            closeMethod = holderType.GetMethod("Close", Type.EmptyTypes);
            openMethod.Invoke(holder, [fullContainerPath]);

            var fileTable = holderType.GetProperty("FileTable")?.GetValue(holder)
                            ?? throw new InvalidOperationException("GRF.Core.GrfHolder.FileTable was not available.");

            var fileTableType = fileTable.GetType();
            var totalEntries = ConvertToInt(fileTableType.GetProperty("Count")?.GetValue(fileTable));
            var directoryCount = CountEnumerable(fileTableType.GetProperty("Directories")?.GetValue(fileTable) as IEnumerable);
            var entriesEnumerable = fileTableType.GetProperty("Entries")?.GetValue(fileTable) as IEnumerable
                                    ?? throw new InvalidOperationException("GRF.Core.FileTable.Entries was not available.");

            var extensionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var topLevelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<GrfContentEntrySnapshot>();

            foreach (var entry in entriesEnumerable)
            {
                if (entry is null)
                {
                    continue;
                }

                var relativePath = ReadString(entry, "RelativePath");
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                var normalizedPath = relativePath.Replace('\\', '/');
                var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
                extensionCounts[extension] = extensionCounts.TryGetValue(extension, out var extensionCount)
                    ? extensionCount + 1
                    : 1;

                var topLevelDirectory = ReadTopLevelDirectory(normalizedPath);
                topLevelCounts[topLevelDirectory] = topLevelCounts.TryGetValue(topLevelDirectory, out var directoryEntryCount)
                    ? directoryEntryCount + 1
                    : 1;

                if (entries.Count >= maxEntries)
                {
                    continue;
                }

                entries.Add(new GrfContentEntrySnapshot(
                    normalizedPath,
                    ReadString(entry, "DirectoryPath"),
                    ReadString(entry, "FileName"),
                    extension,
                    ConvertToInt(ReadProperty(entry, "SizeCompressed")),
                    ConvertToInt(ReadProperty(entry, "SizeDecompressed")),
                    ConvertToBool(ReadProperty(entry, "Encrypted"))));
            }

            var index = GrfContainerContentIndexDocument.Create(
                fullContainerPath,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                totalEntries,
                directoryCount,
                maxEntries,
                totalEntries > entries.Count,
                extensionCounts
                    .OrderByDescending(item => item.Value)
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new GrfExtensionCount(item.Key, item.Value))
                    .ToArray(),
                topLevelCounts
                    .OrderByDescending(item => item.Value)
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .Select(item => new GrfTopLevelDirectoryCount(item.Key, item.Value))
                    .ToArray(),
                entries
                    .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

            if (index.IsTruncated)
            {
                warnings.Add($"Entry capture was truncated to {maxEntries} records; use --limit for a larger sample.");
            }

            return new GrfContainerInspectionResult(
                index,
                "GRF.dll (embedded via GrfCL.exe)",
                hostExecutablePath,
                warnings);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException($"GRF Editor assembly inspection failed: {ex.InnerException.Message}", ex.InnerException);
        }
        finally
        {
            try
            {
                closeMethod?.Invoke(holder, []);
            }
            catch
            {
                warnings.Add("GRF holder close operation returned a non-fatal error.");
            }

            loadContext.Unload();
        }
    }

    private static object? ReadProperty(object target, string propertyName) =>
        target.GetType().GetProperty(propertyName)?.GetValue(target);

    private static string ReadString(object target, string propertyName) =>
        ReadProperty(target, propertyName)?.ToString() ?? string.Empty;

    private static string ReadTopLevelDirectory(string relativePath)
    {
        var separator = relativePath.IndexOf('/');
        return separator <= 0 ? "(root)" : relativePath[..separator];
    }

    private static int ConvertToInt(object? value) =>
        value switch
        {
            null => 0,
            int intValue => intValue,
            long longValue => checked((int)longValue),
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };

    private static bool ConvertToBool(object? value) =>
        value switch
        {
            bool boolValue => boolValue,
            _ => bool.TryParse(value?.ToString(), out var parsed) && parsed
        };

    private static int CountEnumerable(IEnumerable? values)
    {
        if (values is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var _ in values)
        {
            count++;
        }

        return count;
    }
}
