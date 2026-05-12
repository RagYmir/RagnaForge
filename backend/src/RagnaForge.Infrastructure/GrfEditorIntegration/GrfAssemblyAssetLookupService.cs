using System.Collections;
using System.Reflection;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Infrastructure.GrfEditorIntegration;

public sealed class GrfAssemblyAssetLookupService : IGrfAssetLookupService
{
    public GrfAssetLookupResult FindAssets(
        RepositoryPaths paths,
        string resourceName,
        IReadOnlyList<string> extensions,
        GrfAssetLookupOptions options)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);

        var warnings = new List<string>();
        if (!options.Enabled)
        {
            return new GrfAssetLookupResult(resourceName, false, 0, 0, [], [], GrfAssetLookupSource.None);
        }

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return new GrfAssetLookupResult(resourceName, false, 0, 0, [], ["Resource name was empty; GRF asset lookup was skipped."], GrfAssetLookupSource.None);
        }

        var hostExecutablePath = Path.Combine(paths.GrfEditorPath, "GrfCL.exe");
        if (!File.Exists(hostExecutablePath))
        {
            return new GrfAssetLookupResult(resourceName, false, 0, options.ContainerPaths.Count, [], [$"GrfCL.exe was not found in {paths.GrfEditorPath}."], GrfAssetLookupSource.None);
        }

        var containers = options.ContainerPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, options.MaxContainers))
            .ToArray();

        if (containers.Length == 0)
        {
            return new GrfAssetLookupResult(resourceName, false, 0, options.ContainerPaths.Count, [], ["GRF asset lookup was enabled, but no existing containers were provided."], GrfAssetLookupSource.None);
        }

        var normalizedExtensions = extensions
            .Select(extension => extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedNameHints = (options.NameHints ?? [])
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matches = new List<GrfAssetLookupMatch>();
        var maxMatches = Math.Max(1, options.MaxMatchesPerContainer);
        var scanned = 0;

        var loadContext = new GrfEditorAssemblyLoadContext(hostExecutablePath);
        Type? holderType = null;
        MethodInfo? openMethod = null;
        MethodInfo? closeMethod = null;

        try
        {
            var grfAssembly = loadContext.LoadAssembly("GRF");
            holderType = grfAssembly.GetType("GRF.Core.GrfHolder", throwOnError: true)!;
            openMethod = holderType.GetMethod("Open", [typeof(string)])
                         ?? throw new InvalidOperationException("GRF.Core.GrfHolder.Open(string) was not found.");
            closeMethod = holderType.GetMethod("Close", Type.EmptyTypes);

            foreach (var containerPath in containers)
            {
                object? holder = null;
                try
                {
                    holder = Activator.CreateInstance(holderType)
                             ?? throw new InvalidOperationException("Could not instantiate GRF.Core.GrfHolder.");
                    openMethod.Invoke(holder, [containerPath]);
                    scanned++;

                    var entriesEnumerable = holderType.GetProperty("FileTable")?.GetValue(holder)?.GetType().GetProperty("Entries")?.GetValue(holderType.GetProperty("FileTable")?.GetValue(holder)) as IEnumerable;
                    if (entriesEnumerable is null)
                    {
                        warnings.Add($"Could not read entries from {containerPath}.");
                        continue;
                    }

                    var containerMatches = new List<(GrfAssetLookupMatch Match, int Score)>();
                    foreach (var entry in entriesEnumerable)
                    {
                        if (entry is null)
                        {
                            continue;
                        }

                        var relativePath = ReadString(entry, "RelativePath").Replace('\\', '/');
                        var fileName = ReadString(entry, "FileName");
                        var extension = Path.GetExtension(fileName).ToLowerInvariant();
                        if (!normalizedExtensions.Contains(extension))
                        {
                            continue;
                        }

                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                        var isExactMatch = fileNameWithoutExtension.Equals(resourceName, StringComparison.OrdinalIgnoreCase);
                        var score = isExactMatch
                            ? 100
                            : ScoreContainsMatch(fileNameWithoutExtension, relativePath, normalizedNameHints, options.AllowContainsMatch);

                        if (score <= 0)
                        {
                            continue;
                        }

                        containerMatches.Add((new GrfAssetLookupMatch(
                            containerPath,
                            relativePath,
                            extension,
                            ConvertToInt(ReadProperty(entry, "SizeCompressed")),
                            ConvertToInt(ReadProperty(entry, "SizeDecompressed")),
                            ConvertToBool(ReadProperty(entry, "Encrypted"))), score));
                    }

                    foreach (var match in containerMatches
                                 .OrderByDescending(item => item.Score)
                                 .ThenBy(item => item.Match.RelativePath, StringComparer.OrdinalIgnoreCase)
                                 .Take(maxMatches))
                    {
                        matches.Add(match.Match);
                    }

                    if (containerMatches.Count > maxMatches)
                    {
                        warnings.Add($"Match capture for {containerPath} was limited to {maxMatches} result(s).");
                    }
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    warnings.Add($"GRF lookup failed for {containerPath}: {ex.InnerException.Message}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"GRF lookup failed for {containerPath}: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        closeMethod?.Invoke(holder, []);
                    }
                    catch
                    {
                        warnings.Add($"GRF holder close returned a non-fatal error for {containerPath}.");
                    }
                }
            }
        }
        finally
        {
            loadContext.Unload();
        }

        return new GrfAssetLookupResult(
            resourceName,
            true,
            scanned,
            options.ContainerPaths.Count,
            matches,
            warnings,
            GrfAssetLookupSource.LiveScan,
            0,
            scanned);
    }

    private static int ScoreContainsMatch(
        string fileNameWithoutExtension,
        string relativePath,
        IReadOnlyList<string> normalizedNameHints,
        bool allowContainsMatch)
    {
        if (!allowContainsMatch || normalizedNameHints.Count == 0)
        {
            return 0;
        }

        var normalizedFileName = NormalizeLookupToken(fileNameWithoutExtension);
        var normalizedRelativePath = NormalizeLookupToken(relativePath);
        var score = 0;

        foreach (var token in normalizedNameHints)
        {
            if (normalizedFileName.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
            else if (normalizedRelativePath.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }

        return score;
    }

    private static object? ReadProperty(object target, string propertyName) =>
        target.GetType().GetProperty(propertyName)?.GetValue(target);

    private static string ReadString(object target, string propertyName) =>
        ReadProperty(target, propertyName)?.ToString() ?? string.Empty;

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

    private static string NormalizeLookupToken(string value) =>
        new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
