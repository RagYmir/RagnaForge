using System.Text.Json;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Scanning;

/// <summary>
/// Manages the project_index.json cache file.
/// Validates schema, profile, fingerprint and scanRoot for cache invalidation.
/// Ensures cache path stays inside agentRoot — never writes outside.
/// </summary>
public sealed class CacheStore
{
    private const int CurrentSchemaVersion = 1;
    private const string CacheFileName = "project_index.json";

    private readonly string _agentRoot;
    private readonly string _cachePath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CacheStore(string agentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentRoot);
        _agentRoot = PathGuard.Normalize(agentRoot);
        _cachePath = Path.Combine(_agentRoot, "cache", "agent", CacheFileName);
    }

    /// <summary>
    /// Get the full path to the cache file.
    /// </summary>
    public string CachePath => _cachePath;

    /// <summary>
    /// Get the relative path to the cache file from agentRoot.
    /// </summary>
    public string RelativeCachePath => Path.GetRelativePath(_agentRoot, _cachePath);

    /// <summary>
    /// Check if the cache file exists.
    /// </summary>
    public bool CacheExists() => File.Exists(_cachePath);

    /// <summary>
    /// Load existing cache. Returns null if the file does not exist.
    /// </summary>
    public ProjectIndex? Load()
    {
        if (!CacheExists()) return null;

        try
        {
            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<ProjectIndex>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save the index to cache. Ensures the path stays inside agentRoot.
    /// </summary>
    public void Save(ProjectIndex index)
    {
        EnsureCachePathIsSafe();

        var cacheDir = Path.GetDirectoryName(_cachePath)!;
        Directory.CreateDirectory(cacheDir);

        var json = JsonSerializer.Serialize(index, SerializerOptions);
        File.WriteAllText(_cachePath, json);
    }

    /// <summary>
    /// Validate existing cache against current configuration.
    /// Returns (isValid, invalidationReason).
    /// </summary>
    public CacheValidationResult Validate(string activeProfile, string configFingerprint, string scanRoot)
    {
        if (!CacheExists())
            return CacheValidationResult.Invalid("cache_not_found");

        var existing = Load();
        if (existing is null)
            return CacheValidationResult.Invalid("cache_corrupt");

        if (existing.SchemaVersion != CurrentSchemaVersion)
            return CacheValidationResult.Invalid(
                $"schema_version_mismatch (cache={existing.SchemaVersion}, expected={CurrentSchemaVersion})");

        if (!existing.ActiveProfile.Equals(activeProfile, StringComparison.OrdinalIgnoreCase))
            return CacheValidationResult.Invalid(
                $"active_profile_mismatch (cache={existing.ActiveProfile}, current={activeProfile})");

        if (!existing.ConfigFingerprint.Equals(configFingerprint, StringComparison.Ordinal))
            return CacheValidationResult.Invalid(
                $"config_fingerprint_mismatch (cache={existing.ConfigFingerprint[..12]}..., current={configFingerprint[..12]}...)");

        var normalizedScanRoot = PathGuard.Normalize(scanRoot);
        if (!existing.ScanRoot.Equals(normalizedScanRoot, StringComparison.OrdinalIgnoreCase))
            return CacheValidationResult.Invalid(
                $"scan_root_mismatch (cache={existing.ScanRoot}, current={normalizedScanRoot})");

        return CacheValidationResult.Valid();
    }

    /// <summary>
    /// Ensure the cache file path is safely inside the agentRoot.
    /// </summary>
    private void EnsureCachePathIsSafe()
    {
        var normalizedCache = Path.GetFullPath(_cachePath);
        var normalizedAgent = Path.GetFullPath(_agentRoot);

        if (!normalizedCache.StartsWith(normalizedAgent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) &&
            !normalizedCache.Equals(normalizedAgent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cache path '{normalizedCache}' is outside agentRoot '{normalizedAgent}'. Write blocked.");
        }
    }
}

/// <summary>
/// Result of cache validation.
/// </summary>
public sealed class CacheValidationResult
{
    public bool IsValid { get; private init; }
    public string? InvalidationReason { get; private init; }

    public static CacheValidationResult Valid() => new() { IsValid = true };
    public static CacheValidationResult Invalid(string reason) =>
        new() { IsValid = false, InvalidationReason = reason };
}
