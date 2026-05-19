using System.Text.Json;

namespace RagnaForge.Agent.Core.Configuration;

/// <summary>
/// Loads and validates all configuration files.
/// Never uses hardcoded path fallbacks — fails explicitly if config is missing.
/// </summary>
public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _configDir;

    public ConfigLoader(string configDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDir);
        _configDir = configDir;
    }

    /// <summary>
    /// Load ragnaforge.agent.json.
    /// </summary>
    public AgentConfig LoadAgentConfig()
    {
        var path = Path.Combine(_configDir, "ragnaforge.agent.json");
        return LoadAndValidate<AgentConfig>(path, "ragnaforge.agent.json");
    }

    /// <summary>
    /// Load paths.json and validate activeProfile exists.
    /// </summary>
    public PathsConfig LoadPathsConfig()
    {
        var path = Path.Combine(_configDir, "paths.json");
        var config = LoadAndValidate<PathsConfig>(path, "paths.json");

        if (string.IsNullOrWhiteSpace(config.AgentRoot))
            throw new InvalidOperationException("paths.json: 'agentRoot' is required.");

        if (string.IsNullOrWhiteSpace(config.ActiveProfile))
            throw new InvalidOperationException("paths.json: 'activeProfile' is required.");

        if (config.Profiles.Count == 0)
            throw new InvalidOperationException("paths.json: at least one profile must be defined.");

        if (!config.Profiles.ContainsKey(config.ActiveProfile))
            throw new InvalidOperationException(
                $"paths.json: activeProfile '{config.ActiveProfile}' does not exist in profiles.");

        foreach (var (name, profile) in config.Profiles)
        {
            var dbMode = profile.DbMode.Trim().ToLowerInvariant();
            if (dbMode is not ("renewal" or "pre-renewal" or "hybrid"))
            {
                throw new InvalidOperationException(
                    $"paths.json: profile '{name}' has invalid dbMode '{profile.DbMode}'. Use renewal, pre-renewal or hybrid.");
            }
        }

        return config;
    }

    /// <summary>
    /// Load safety.json.
    /// </summary>
    public SafetyConfig LoadSafetyConfig()
    {
        var path = Path.Combine(_configDir, "safety.json");
        return LoadAndValidate<SafetyConfig>(path, "safety.json");
    }

    /// <summary>
    /// Get the resolved active profile from a loaded PathsConfig.
    /// </summary>
    public static ProfileConfig GetActiveProfile(PathsConfig pathsConfig)
    {
        ArgumentNullException.ThrowIfNull(pathsConfig);

        if (!pathsConfig.Profiles.TryGetValue(pathsConfig.ActiveProfile, out var profile))
            throw new InvalidOperationException(
                $"Active profile '{pathsConfig.ActiveProfile}' not found in profiles dictionary.");

        return profile;
    }

    private T LoadAndValidate<T>(string filePath, string friendlyName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"Configuration file '{friendlyName}' not found at: {filePath}", filePath);

        var json = File.ReadAllText(filePath);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                $"Configuration file '{friendlyName}' is empty at: {filePath}");

        var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

        if (result is null)
            throw new InvalidOperationException(
                $"Configuration file '{friendlyName}' deserialized to null at: {filePath}");

        return result;
    }
}
