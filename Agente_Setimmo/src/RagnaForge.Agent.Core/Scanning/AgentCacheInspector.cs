using System.Text.Json;
using RagnaForge.Agent.Core.Entities;

namespace RagnaForge.Agent.Core.Scanning;

public sealed class CacheTrustDetails
{
    public bool CacheTrusted { get; set; }
    public string? CacheStaleReason { get; set; }
    public string? CacheFingerprint { get; set; }
    public string? ActiveFingerprint { get; set; }
    public string? CacheProfile { get; set; }
    public string? ActiveProfile { get; set; }
    public string RecommendedAction { get; set; } = "none";
}

public sealed class CacheInspectionResult<T>
{
    public T? Document { get; set; }
    public CacheTrustDetails Details { get; set; } = new();
}

public static class AgentCacheInspector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CacheInspectionResult<ProjectIndex> InspectProjectIndex(
        string agentRoot,
        string activeProfile,
        string activeFingerprint,
        string scanRoot)
    {
        var cachePath = Path.Combine(agentRoot, "cache", "agent", "project_index.json");
        return InspectCache<ProjectIndex>(
            cachePath,
            activeProfile,
            activeFingerprint,
            static document => document.ActiveProfile,
            static document => document.ConfigFingerprint,
            static document => document.ScanRoot,
            scanRoot);
    }

    public static CacheInspectionResult<EntityIndex> InspectEntityIndex(
        string agentRoot,
        string activeProfile,
        string activeFingerprint,
        string scope = "entities")
    {
        var fileName = scope == "entities" ? "entities_index.json" : $"{scope.TrimEnd('s')}_index.json";
        var cachePath = Path.Combine(agentRoot, "cache", "agent", fileName);
        return InspectCache<EntityIndex>(
            cachePath,
            activeProfile,
            activeFingerprint,
            static document => document.ActiveProfile,
            static document => document.ConfigFingerprint);
    }

    private static CacheInspectionResult<T> InspectCache<T>(
        string cachePath,
        string activeProfile,
        string activeFingerprint,
        Func<T, string?> profileSelector,
        Func<T, string?> fingerprintSelector,
        Func<T, string?>? rootSelector = null,
        string? expectedRoot = null)
    {
        var result = new CacheInspectionResult<T>
        {
            Details = new CacheTrustDetails
            {
                CacheTrusted = false,
                ActiveFingerprint = activeFingerprint,
                ActiveProfile = activeProfile,
                RecommendedAction = "run_scan_or_index"
            }
        };

        if (!File.Exists(cachePath))
        {
            result.Details.CacheStaleReason = "cache_not_found";
            return result;
        }

        try
        {
            var document = JsonSerializer.Deserialize<T>(File.ReadAllText(cachePath), JsonOptions);
            if (document is null)
            {
                result.Details.CacheStaleReason = "cache_corrupt";
                return result;
            }

            var cacheProfile = profileSelector(document);
            var cacheFingerprint = fingerprintSelector(document);
            result.Details.CacheProfile = cacheProfile;
            result.Details.CacheFingerprint = cacheFingerprint;

            if (!string.Equals(cacheProfile, activeProfile, StringComparison.OrdinalIgnoreCase))
            {
                result.Details.CacheStaleReason = "activeProfile_mismatch";
                return result;
            }

            if (!string.Equals(cacheFingerprint, activeFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                result.Details.CacheStaleReason = "configFingerprint_mismatch";
                return result;
            }

            if (rootSelector is not null && !string.IsNullOrWhiteSpace(expectedRoot))
            {
                var cacheRoot = rootSelector(document) ?? string.Empty;
                var normalizedExpectedRoot = Path.GetFullPath(expectedRoot);
                var normalizedCacheRoot = string.IsNullOrWhiteSpace(cacheRoot)
                    ? string.Empty
                    : Path.GetFullPath(cacheRoot);

                if (!string.Equals(normalizedCacheRoot, normalizedExpectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    result.Details.CacheStaleReason = "scanRoot_mismatch";
                    return result;
                }
            }

            result.Document = document;
            result.Details.CacheTrusted = true;
            result.Details.CacheStaleReason = null;
            result.Details.RecommendedAction = "none";
            return result;
        }
        catch
        {
            result.Details.CacheStaleReason = "cache_corrupt";
            return result;
        }
    }
}
