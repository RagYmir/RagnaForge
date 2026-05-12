using System.Collections;
using System.Reflection;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Infrastructure.GrfEditorIntegration;

public sealed class GrfAssemblyFileExtractor : IGrfFileExtractor
{
    public GrfFileExtractionResult ExtractFiles(
        RepositoryPaths paths,
        IReadOnlyList<GrfAssetLookupMatch> matches,
        string extractionRoot,
        long maxFileBytes)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(matches);

        var warnings = new List<string>();
        var extracted = new List<GrfExtractedFile>();
        var fullExtractionRoot = Path.GetFullPath(extractionRoot);
        Directory.CreateDirectory(fullExtractionRoot);

        if (matches.Count == 0)
        {
            return new GrfFileExtractionResult(false, fullExtractionRoot, [], []);
        }

        var hostExecutablePath = Path.Combine(paths.GrfEditorPath, "GrfCL.exe");
        if (!File.Exists(hostExecutablePath))
        {
            return new GrfFileExtractionResult(
                true,
                fullExtractionRoot,
                [],
                [$"GrfCL.exe was not found in {paths.GrfEditorPath}."]);
        }

        var loadContext = new GrfEditorAssemblyLoadContext(hostExecutablePath);
        Type? holderType = null;
        MethodInfo? openMethod = null;
        MethodInfo? closeMethod = null;
        MethodInfo? getDecompressedDataMethod = null;

        try
        {
            var grfAssembly = loadContext.LoadAssembly("GRF");
            holderType = grfAssembly.GetType("GRF.Core.GrfHolder", throwOnError: true)!;
            var fileEntryType = grfAssembly.GetType("GRF.Core.FileEntry", throwOnError: true)!;
            openMethod = holderType.GetMethod("Open", [typeof(string)])
                         ?? throw new InvalidOperationException("GRF.Core.GrfHolder.Open(string) was not found.");
            closeMethod = holderType.GetMethod("Close", Type.EmptyTypes);
            getDecompressedDataMethod = fileEntryType.GetMethod("GetDecompressedData", Type.EmptyTypes)
                                        ?? throw new InvalidOperationException("GRF.Core.FileEntry.GetDecompressedData() was not found.");

            var ordinal = 0;
            foreach (var containerGroup in matches
                         .Where(match => File.Exists(match.ContainerPath))
                         .GroupBy(match => Path.GetFullPath(match.ContainerPath), StringComparer.OrdinalIgnoreCase))
            {
                object? holder = null;
                try
                {
                    holder = Activator.CreateInstance(holderType)
                             ?? throw new InvalidOperationException("Could not instantiate GRF.Core.GrfHolder.");
                    openMethod.Invoke(holder, [containerGroup.Key]);

                    var fileTable = holderType.GetProperty("FileTable")?.GetValue(holder);
                    if (fileTable is null)
                    {
                        warnings.Add($"Could not read FileTable from {containerGroup.Key}.");
                        continue;
                    }

                    foreach (var match in containerGroup)
                    {
                        if (match.SizeDecompressed > maxFileBytes)
                        {
                            warnings.Add($"Skipped {match.RelativePath} because it is larger than the controlled extraction limit.");
                            continue;
                        }

                        var entry = ResolveEntry(fileTable, match.RelativePath);
                        if (entry is null)
                        {
                            warnings.Add($"Could not resolve GRF entry {match.RelativePath} in {containerGroup.Key}.");
                            continue;
                        }

                        var data = getDecompressedDataMethod.Invoke(entry, []) as byte[];
                        if (data is null)
                        {
                            warnings.Add($"Could not decompress GRF entry {match.RelativePath} in {containerGroup.Key}.");
                            continue;
                        }

                        if (data.LongLength > maxFileBytes)
                        {
                            warnings.Add($"Skipped {match.RelativePath} because decompressed size exceeds the controlled extraction limit.");
                            continue;
                        }

                        var targetPath = Path.Combine(
                            fullExtractionRoot,
                            $"{ordinal:0000}_{SanitizeFileName(Path.GetFileName(match.RelativePath))}");
                        EnsureInsideRoot(fullExtractionRoot, targetPath);
                        File.WriteAllBytes(targetPath, data);
                        extracted.Add(new GrfExtractedFile(
                            containerGroup.Key,
                            match.RelativePath,
                            targetPath,
                            data.LongLength));
                        ordinal++;
                    }
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    warnings.Add($"GRF extraction failed for {containerGroup.Key}: {ex.InnerException.Message}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"GRF extraction failed for {containerGroup.Key}: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        closeMethod?.Invoke(holder, []);
                    }
                    catch
                    {
                        warnings.Add($"GRF holder close returned a non-fatal error for {containerGroup.Key}.");
                    }
                }
            }
        }
        finally
        {
            loadContext.Unload();
        }

        return new GrfFileExtractionResult(true, fullExtractionRoot, extracted, warnings);
    }

    private static object? ResolveEntry(object fileTable, string relativePath)
    {
        var fileTableType = fileTable.GetType();
        var tryGetMethod = fileTableType.GetMethod("TryGet", [typeof(string)]);
        var direct = tryGetMethod?.Invoke(fileTable, [relativePath])
                     ?? tryGetMethod?.Invoke(fileTable, [relativePath.Replace('/', '\\')])
                     ?? tryGetMethod?.Invoke(fileTable, [relativePath.Replace('\\', '/')]);
        if (direct is not null)
        {
            return direct;
        }

        var entriesEnumerable = fileTableType.GetProperty("Entries")?.GetValue(fileTable) as IEnumerable;
        if (entriesEnumerable is null)
        {
            return null;
        }

        foreach (var entry in entriesEnumerable)
        {
            if (entry is null)
            {
                continue;
            }

            var candidatePath = entry.GetType().GetProperty("RelativePath")?.GetValue(entry)?.ToString();
            if (candidatePath is not null
                && candidatePath.Replace('\\', '/').Equals(relativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static void EnsureInsideRoot(string root, string path)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.GetFullPath(path);
        var fullRootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(fullRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to extract outside controlled root: {fullPath}");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(value => invalid.Contains(value) ? '_' : value).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "extracted.bin" : sanitized;
    }
}
