using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge config get/validate/set'. Config management.
/// </summary>
public sealed class ConfigCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _subCommand; // get, validate, set
    private readonly string? _key;
    private readonly string? _value;

    private static readonly HashSet<string> AllowedSetKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ragnaforgeMainProjectPath",
        "rathenaPath",
        "patchPath",
        "grfRepositoryPath",
        "grfEditorPath"
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigCommand(string configDir, string agentRoot, string subCommand, string? key = null, string? value = null)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _subCommand = subCommand;
        _key = key;
        _value = value;
    }

    public JsonOutput Execute()
    {
        return _subCommand switch
        {
            "get" => ExecuteGet(),
            "validate" => ExecuteValidate(),
            "set" => ExecuteSet(),
            _ => JsonOutput.Error("config", $"Unknown sub-command: {_subCommand}. Use get/validate/set.")
        };
    }

    private JsonOutput ExecuteGet()
    {
        var output = JsonOutput.Success("config");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var agentConfig = loader.LoadAgentConfig();
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;
            output.Summary = $"Configuration for profile '{pathsConfig.ActiveProfile}'.";
            output.Data = new
            {
                agentName = agentConfig.AgentName,
                activeProfile = pathsConfig.ActiveProfile,
                configFingerprint = fingerprint,
                paths = new
                {
                    profile.RagnaforgeMainProjectPath,
                    profile.RathenaPath,
                    profile.PatchPath,
                    profile.GrfRepositoryPath,
                    profile.GrfEditorPath,
                    profile.DbMode
                },
                safety = safetyConfig,
                availableProfiles = pathsConfig.Profiles.Keys.ToList()
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("config", ex.Message);
        }

        return output;
    }

    private JsonOutput ExecuteValidate()
    {
        var output = JsonOutput.Success("config");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            var issues = PathGuard.EnsureProfileIsSafe(profile);
            if (issues.Count > 0)
            {
                output.Ok = false;
                output.Errors.AddRange(issues);
                output.SafeForAutomation = false;
                output.Summary = $"Configuration validation failed - {issues.Count} issue(s).";
            }
            else
            {
                output.Summary = "Configuration validation passed.";
            }

            output.Data = new { valid = issues.Count == 0, issues };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("config", ex.Message);
        }

        return output;
    }

    private JsonOutput ExecuteSet()
    {
        var output = JsonOutput.Success("config");
        try
        {
            if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_value))
            {
                return JsonOutput.Error("config", "Usage: ragnaforge config set <key> <value>");
            }

            if (!AllowedSetKeys.Contains(_key))
            {
                return JsonOutput.Error("config", $"Key '{_key}' is not allowed. Allowed: {string.Join(", ", AllowedSetKeys)}");
            }

            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var oldFingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;

            var candidate = CloneProfile(profile);
            ApplyKeyValue(candidate, _key, _value);

            var preflight = ValidateConfigSetPreflight(_key, _value, candidate);
            if (!preflight.Passed)
            {
                output = JsonOutput.Error("config", "Path rejected by preflight validation.");
                output.ActiveProfile = pathsConfig.ActiveProfile;
                output.ConfigFingerprint = oldFingerprint;
                output.NextRequiredAction = "choose_valid_path";
                output.Data = new
                {
                    key = _key,
                    value = _value,
                    preflight = new
                    {
                        passed = false,
                        checks = preflight.Checks
                    }
                };
                return output;
            }

            BackupPathsFile();
            CopyProfile(candidate, profile);

            var issues = PathGuard.EnsureProfileIsSafe(profile);
            if (issues.Count > 0)
            {
                output = JsonOutput.Error("config", "Config rejected by safety validation after preflight.");
                output.ActiveProfile = pathsConfig.ActiveProfile;
                output.ConfigFingerprint = oldFingerprint;
                output.Errors.AddRange(issues);
                output.NextRequiredAction = "choose_valid_path";
                output.Data = new
                {
                    key = _key,
                    value = _value,
                    preflight = new
                    {
                        passed = true,
                        checks = preflight.Checks
                    }
                };
                return output;
            }

            File.WriteAllText(Path.Combine(_configDir, "paths.json"), JsonSerializer.Serialize(pathsConfig, JsonOpts));

            var newFingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ConfigFingerprint = newFingerprint;
            output.Summary = $"Config '{_key}' updated after preflight validation.";
            output.NextRequiredAction = "run_baseline";
            output.Data = new
            {
                key = _key,
                value = _value,
                oldFingerprint,
                newFingerprint,
                preflight = new
                {
                    passed = true,
                    checks = preflight.Checks
                },
                cacheInvalidated = !string.Equals(oldFingerprint, newFingerprint, StringComparison.OrdinalIgnoreCase)
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("config", ex.Message);
        }

        return output;
    }

    private void BackupPathsFile()
    {
        var pathsFile = Path.Combine(_configDir, "paths.json");
        var backupDir = Path.Combine(_agentRoot, "cache", "agent", "config_backups");
        Directory.CreateDirectory(backupDir);
        File.Copy(
            pathsFile,
            Path.Combine(backupDir, $"paths_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.json"),
            overwrite: true);
    }

    private static ProfileConfig CloneProfile(ProfileConfig source) => new()
    {
        RagnaforgeMainProjectPath = source.RagnaforgeMainProjectPath,
        RathenaPath = source.RathenaPath,
        PatchPath = source.PatchPath,
        GrfRepositoryPath = source.GrfRepositoryPath,
        GrfEditorPath = source.GrfEditorPath,
        DbMode = source.DbMode,
        WritableRoots = [.. source.WritableRoots],
        ReadOnlyRoots = [.. source.ReadOnlyRoots]
    };

    private static void CopyProfile(ProfileConfig source, ProfileConfig destination)
    {
        destination.RagnaforgeMainProjectPath = source.RagnaforgeMainProjectPath;
        destination.RathenaPath = source.RathenaPath;
        destination.PatchPath = source.PatchPath;
        destination.GrfRepositoryPath = source.GrfRepositoryPath;
        destination.GrfEditorPath = source.GrfEditorPath;
        destination.DbMode = source.DbMode;
        destination.WritableRoots = [.. source.WritableRoots];
        destination.ReadOnlyRoots = [.. source.ReadOnlyRoots];
    }

    private static void ApplyKeyValue(ProfileConfig profile, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "ragnaforgemainprojectpath":
                profile.RagnaforgeMainProjectPath = value;
                break;
            case "rathenapath":
                profile.RathenaPath = value;
                break;
            case "patchpath":
                profile.PatchPath = value;
                break;
            case "grfrepositorypath":
                profile.GrfRepositoryPath = value;
                break;
            case "grfeditorpath":
                profile.GrfEditorPath = value;
                break;
        }
    }

    private static ConfigSetPreflightResult ValidateConfigSetPreflight(string key, string value, ProfileConfig candidate)
    {
        var result = new ConfigSetPreflightResult();
        var normalizedValue = Path.GetFullPath(value);

        result.Checks.Add(Directory.Exists(normalizedValue) || File.Exists(normalizedValue)
            ? "path_exists"
            : "path_missing");
        if (!Directory.Exists(normalizedValue) && !File.Exists(normalizedValue))
        {
            result.Errors.Add("Configured path does not exist.");
            return result;
        }

        if (Directory.Exists(normalizedValue))
        {
            try
            {
                _ = Directory.EnumerateFileSystemEntries(normalizedValue).Take(1).ToList();
                result.Checks.Add("path_readable");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configured path is not readable: {ex.Message}");
                return result;
            }
        }
        else
        {
            try
            {
                using var stream = File.Open(normalizedValue, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                result.Checks.Add("path_readable");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Configured file path is not readable: {ex.Message}");
                return result;
            }
        }

        var pathSafetyIssues = PathGuard.EnsureProfileIsSafe(candidate);
        if (pathSafetyIssues.Count > 0)
        {
            result.Errors.AddRange(pathSafetyIssues);
            return result;
        }

        var guard = new PathGuard(candidate.WritableRoots, candidate.ReadOnlyRoots);
        var readCheck = guard.EnsureCanRead(normalizedValue);
        if (!readCheck.IsAllowed)
        {
            result.Errors.Add($"Path rejected by PathGuard: {readCheck.Reason}");
            return result;
        }

        result.Checks.Add("pathguard_read_ok");

        switch (key.ToLowerInvariant())
        {
            case "ragnaforgemainprojectpath":
                ValidateRagnaforgeProjectPath(result, normalizedValue);
                break;
            case "rathenapath":
                ValidateRathenaPath(result, normalizedValue);
                break;
            case "patchpath":
                ValidatePatchPath(result, normalizedValue);
                break;
            case "grfrepositorypath":
                ValidateGrfRepositoryPath(result, normalizedValue, candidate);
                break;
            case "grfeditorpath":
                ValidateGrfEditorPath(result, normalizedValue);
                break;
        }

        return result;
    }

    private static void ValidateRagnaforgeProjectPath(ConfigSetPreflightResult result, string path)
    {
        if (!Directory.Exists(Path.Combine(path, "backend")))
        {
            result.Errors.Add("ragnaforgeMainProjectPath must contain backend/.");
        }

        if (!Directory.Exists(Path.Combine(path, "frontend")))
        {
            result.Errors.Add("ragnaforgeMainProjectPath must contain frontend/.");
        }

        var hasSolution = Directory.EnumerateFiles(path, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                          Directory.EnumerateFiles(path, "*.slnx", SearchOption.TopDirectoryOnly).Any();
        if (!hasSolution)
        {
            result.Errors.Add("ragnaforgeMainProjectPath must contain a solution file (.sln or .slnx).");
        }

        if (result.Errors.Count == 0)
        {
            result.Checks.Add("ragnaforge_structure_ok");
        }
    }

    private static void ValidateRathenaPath(ConfigSetPreflightResult result, string path)
    {
        if (!Directory.Exists(Path.Combine(path, "db")))
        {
            result.Errors.Add("rathenaPath must contain db/.");
        }

        if (!Directory.Exists(Path.Combine(path, "npc")) && !Directory.Exists(Path.Combine(path, "conf")))
        {
            result.Errors.Add("rathenaPath must contain npc/ or conf/.");
        }

        if (result.Errors.Count == 0)
        {
            result.Checks.Add("rathena_structure_ok");
        }
    }

    private static void ValidatePatchPath(ConfigSetPreflightResult result, string path)
    {
        if (!Directory.Exists(path))
        {
            result.Errors.Add("patchPath must be a directory.");
            return;
        }

        if (!Directory.Exists(Path.Combine(path, "data")))
        {
            result.Checks.Add("patch_structure_warning_no_data_dir");
        }
        else
        {
            result.Checks.Add("patch_structure_ok");
        }
    }

    private static void ValidateGrfRepositoryPath(ConfigSetPreflightResult result, string path, ProfileConfig candidate)
    {
        if (!Directory.Exists(path))
        {
            result.Errors.Add("grfRepositoryPath must be a directory.");
            return;
        }

        var normalizedPath = PathGuard.Normalize(path);
        var writableOverlap = candidate.WritableRoots.Any(root =>
            PathGuard.IsContainedIn(normalizedPath, PathGuard.Normalize(root)) ||
            PathGuard.IsContainedIn(PathGuard.Normalize(root), normalizedPath));
        if (writableOverlap)
        {
            result.Errors.Add("grfRepositoryPath cannot overlap writableRoots.");
            return;
        }

        var coveredByReadOnlyRoot = candidate.ReadOnlyRoots.Any(root =>
            PathGuard.IsContainedIn(normalizedPath, PathGuard.Normalize(root)));
        if (!coveredByReadOnlyRoot)
        {
            candidate.ReadOnlyRoots.Add(normalizedPath);
            result.Checks.Add("grf_readonly_root_added");
        }
        else
        {
            result.Checks.Add("grf_readonly_root_ok");
        }
    }

    private static void ValidateGrfEditorPath(ConfigSetPreflightResult result, string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            result.Errors.Add("grfEditorPath must point to an existing directory or executable.");
            return;
        }

        result.Checks.Add("grf_editor_path_ok");
    }

    private sealed class ConfigSetPreflightResult
    {
        public List<string> Checks { get; } = [];
        public List<string> Errors { get; } = [];
        public bool Passed => Errors.Count == 0;
    }
}

/// <summary>
/// Implements 'ragnaforge profile list/use/validate'.
/// </summary>
public sealed class ProfileCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _subCommand; // list, use, validate
    private readonly string? _profileName;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileCommand(string configDir, string agentRoot, string subCommand, string? profileName = null)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _subCommand = subCommand;
        _profileName = profileName;
    }

    public JsonOutput Execute()
    {
        return _subCommand switch
        {
            "list" => ExecuteList(),
            "use" => ExecuteUse(),
            "validate" => ExecuteValidate(),
            _ => JsonOutput.Error("profile", $"Unknown sub-command: {_subCommand}. Use list/use/validate.")
        };
    }

    private JsonOutput ExecuteList()
    {
        var output = JsonOutput.Success("profile");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.Summary = $"Found {pathsConfig.Profiles.Count} profile(s).";
            output.Data = new
            {
                activeProfile = pathsConfig.ActiveProfile,
                profiles = pathsConfig.Profiles.Keys.ToList()
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("profile", ex.Message);
        }

        return output;
    }

    private JsonOutput ExecuteUse()
    {
        var output = JsonOutput.Success("profile");
        try
        {
            if (string.IsNullOrWhiteSpace(_profileName))
            {
                return JsonOutput.Error("profile", "Usage: ragnaforge profile use <name>");
            }

            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var oldFingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);

            if (!pathsConfig.Profiles.ContainsKey(_profileName))
            {
                return JsonOutput.Error(
                    "profile",
                    $"Profile '{_profileName}' does not exist. Available: {string.Join(", ", pathsConfig.Profiles.Keys)}");
            }

            var targetProfile = pathsConfig.Profiles[_profileName];
            var issues = PathGuard.EnsureProfileIsSafe(targetProfile);
            if (issues.Count > 0)
            {
                output.Ok = false;
                output.Errors.AddRange(issues);
                output.Errors.Add($"Profile '{_profileName}' NOT activated - safety validation failed.");
                return output;
            }

            var pathsFile = Path.Combine(_configDir, "paths.json");
            var backupDir = Path.Combine(_agentRoot, "cache", "agent", "config_backups");
            Directory.CreateDirectory(backupDir);
            File.Copy(pathsFile, Path.Combine(backupDir, $"paths_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.json"), true);

            pathsConfig.ActiveProfile = _profileName;
            File.WriteAllText(pathsFile, JsonSerializer.Serialize(pathsConfig, JsonOpts));

            var newFingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ActiveProfile = _profileName;
            output.ConfigFingerprint = newFingerprint;
            output.Summary = $"Switched to profile '{_profileName}'.";
            output.Data = new { activeProfile = _profileName, oldFingerprint, newFingerprint };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("profile", ex.Message);
        }

        return output;
    }

    private JsonOutput ExecuteValidate()
    {
        var output = JsonOutput.Success("profile");
        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);

            var issues = PathGuard.EnsureProfileIsSafe(profile);
            output.Summary = issues.Count == 0
                ? $"Profile '{pathsConfig.ActiveProfile}' is safe."
                : $"Profile '{pathsConfig.ActiveProfile}' has {issues.Count} issue(s).";
            if (issues.Count > 0)
            {
                output.Ok = false;
                output.Errors.AddRange(issues);
                output.SafeForAutomation = false;
            }

            output.Data = new { valid = issues.Count == 0, issues };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("profile", ex.Message);
        }

        return output;
    }
}
