using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RagnaForge.Agent.Core.Configuration;

/// <summary>
/// Generates a deterministic fingerprint of the active configuration.
/// Used to detect when cache becomes obsolete due to config/path/profile changes.
/// </summary>
public static class ConfigFingerprint
{
    /// <summary>
    /// Generate a SHA-256 fingerprint from the active profile and safety settings.
    /// Any change in paths, profile name, writable/readOnly roots or relevant safety
    /// settings will produce a different fingerprint, invalidating old caches.
    /// </summary>
    public static string Generate(PathsConfig pathsConfig, SafetyConfig safetyConfig)
    {
        ArgumentNullException.ThrowIfNull(pathsConfig);
        ArgumentNullException.ThrowIfNull(safetyConfig);

        var profile = ConfigLoader.GetActiveProfile(pathsConfig);

        var payload = new Dictionary<string, object?>
        {
            ["activeProfile"] = pathsConfig.ActiveProfile,
            ["agentRoot"] = pathsConfig.AgentRoot,
            ["ragnaforgeMainProjectPath"] = profile.RagnaforgeMainProjectPath,
            ["rathenaPath"] = profile.RathenaPath,
            ["patchPath"] = profile.PatchPath,
            ["grfRepositoryPath"] = profile.GrfRepositoryPath,
            ["grfEditorPath"] = profile.GrfEditorPath,
            ["dbMode"] = profile.DbMode,
            ["writableRoots"] = profile.WritableRoots,
            ["readOnlyRoots"] = profile.ReadOnlyRoots,
            ["blockOriginalGrfWrite"] = safetyConfig.BlockOriginalGrfWrite,
            ["blockLubEditing"] = safetyConfig.BlockLubEditing,
            ["requireDryRunBeforeApply"] = safetyConfig.RequireDryRunBeforeApply,
            ["requireDiffBeforeApply"] = safetyConfig.RequireDiffBeforeApply,
            ["requireExplicitConfirmation"] = safetyConfig.RequireExplicitConfirmation,
            ["cacheMustMatchActiveProfile"] = safetyConfig.CacheMustMatchActiveProfile,
            ["invalidateCacheOnPathChange"] = safetyConfig.InvalidateCacheOnPathChange
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(hashBytes);
    }
}
