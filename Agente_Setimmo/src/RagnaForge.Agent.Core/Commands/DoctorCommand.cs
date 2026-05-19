using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements the 'ragnaforge doctor --json' command.
/// Validates configs, PathGuard, logs, cache, profiles, safety gates and protections.
/// Read-only: never modifies external files.
/// </summary>
public sealed class DoctorCommand
{
    private readonly string _configDir;

    public DoctorCommand(string configDir)
    {
        _configDir = configDir;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("doctor");
        var checks = new List<object>();

        try
        {
            // 1. Validate config files exist
            checks.Add(CheckFileExists("ragnaforge.agent.json"));
            checks.Add(CheckFileExists("paths.json"));
            checks.Add(CheckFileExists("safety.json"));

            // 2. Load configs
            var loader = new ConfigLoader(_configDir);

            AgentConfig? agentConfig = null;
            PathsConfig? pathsConfig = null;
            SafetyConfig? safetyConfig = null;

            try
            {
                agentConfig = loader.LoadAgentConfig();
                checks.Add(Pass("config.agent", "ragnaforge.agent.json loaded successfully."));
            }
            catch (Exception ex)
            {
                checks.Add(Fail("config.agent", ex.Message));
            }

            try
            {
                pathsConfig = loader.LoadPathsConfig();
                checks.Add(Pass("config.paths", "paths.json loaded successfully."));
            }
            catch (Exception ex)
            {
                checks.Add(Fail("config.paths", ex.Message));
            }

            try
            {
                safetyConfig = loader.LoadSafetyConfig();
                checks.Add(Pass("config.safety", "safety.json loaded successfully."));
            }
            catch (Exception ex)
            {
                checks.Add(Fail("config.safety", ex.Message));
            }

            if (pathsConfig is null || safetyConfig is null)
            {
                output.Errors.Add("Cannot complete doctor without valid paths.json and safety.json.");
                output.Ok = false;
                output.Data = new { checks };
                return output;
            }

            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);
            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            // 3. Validate activeProfile
            checks.Add(Pass("profile.active",
                $"Active profile '{pathsConfig.ActiveProfile}' found."));

            // 4. Validate agentRoot exists
            if (Directory.Exists(pathsConfig.AgentRoot))
                checks.Add(Pass("dir.agentRoot", $"agentRoot exists: {pathsConfig.AgentRoot}"));
            else
                checks.Add(Fail("dir.agentRoot",
                    $"agentRoot does not exist: {pathsConfig.AgentRoot}"));

            // 5. Validate profile paths exist
            ValidatePathExists(checks, "dir.ragnaforgeMainProjectPath",
                profile.RagnaforgeMainProjectPath);
            ValidatePathExists(checks, "dir.rathenaPath", profile.RathenaPath);
            ValidatePathExists(checks, "dir.patchPath", profile.PatchPath);
            ValidatePathExists(checks, "dir.grfRepositoryPath", profile.GrfRepositoryPath);

            // 6. Validate PathGuard safety — issues are ERRORS, not warnings
            var profileIssues = PathGuard.EnsureProfileIsSafe(profile);
            if (profileIssues.Count == 0)
            {
                checks.Add(Pass("security.profileSafety",
                    "Profile path configuration is safe."));
            }
            else
            {
                foreach (var issue in profileIssues)
                {
                    checks.Add(Fail("security.profileSafety", issue));
                    output.Errors.Add(issue);
                }
            }

            // 7. Validate GRF is read-only
            var grfIssues = PathGuard.EnsureGrfRepositoryIsReadOnly(profile);
            if (grfIssues.Count == 0)
            {
                checks.Add(Pass("security.grfReadOnly",
                    "grfRepositoryPath is correctly in readOnlyRoots."));
            }
            else
            {
                foreach (var issue in grfIssues)
                {
                    checks.Add(Fail("security.grfReadOnly", issue));
                    output.Errors.Add(issue);
                }
            }

            // 8. Validate all critical safety gates
            ValidateSafetyGate(checks, output, "safety.requireDryRunBeforeApply",
                safetyConfig.RequireDryRunBeforeApply, "requireDryRunBeforeApply");
            ValidateSafetyGate(checks, output, "safety.requireDiffBeforeApply",
                safetyConfig.RequireDiffBeforeApply, "requireDiffBeforeApply");
            ValidateSafetyGate(checks, output, "safety.requireValidationBeforeApply",
                safetyConfig.RequireValidationBeforeApply, "requireValidationBeforeApply");
            ValidateSafetyGate(checks, output, "safety.requireExplicitConfirmation",
                safetyConfig.RequireExplicitConfirmation, "requireExplicitConfirmation");
            ValidateSafetyGate(checks, output, "safety.backupBeforeApply",
                safetyConfig.BackupBeforeApply, "backupBeforeApply");
            ValidateSafetyGate(checks, output, "safety.blockOriginalGrfWrite",
                safetyConfig.BlockOriginalGrfWrite, "blockOriginalGrfWrite");
            ValidateSafetyGate(checks, output, "safety.blockLubEditing",
                safetyConfig.BlockLubEditing, "blockLubEditing");
            ValidateSafetyGate(checks, output, "safety.invalidateCacheOnPathChange",
                safetyConfig.InvalidateCacheOnPathChange, "invalidateCacheOnPathChange");
            ValidateSafetyGate(checks, output, "safety.cacheMustMatchActiveProfile",
                safetyConfig.CacheMustMatchActiveProfile, "cacheMustMatchActiveProfile");

            // 9. Validate log directories
            var logsDir = Path.Combine(pathsConfig.AgentRoot, "logs");
            if (Directory.Exists(logsDir))
                checks.Add(Pass("dir.logs", $"Logs directory exists: {logsDir}"));
            else
                checks.Add(Warn("dir.logs",
                    $"Logs directory does not exist: {logsDir}"));

            // 10. Validate cache directory
            var cacheDir = Path.Combine(pathsConfig.AgentRoot, "cache", "agent");
            if (Directory.Exists(cacheDir))
                checks.Add(Pass("dir.cache", $"Cache directory exists: {cacheDir}"));
            else
                checks.Add(Warn("dir.cache",
                    $"Cache directory does not exist: {cacheDir}"));

            // 11. Detect obsolete cache
            var cacheIndexPath = Path.Combine(cacheDir, "project_index.json");
            if (File.Exists(cacheIndexPath))
            {
                try
                {
                    var cacheContent = File.ReadAllText(cacheIndexPath);
                    if (cacheContent.Contains(fingerprint))
                    {
                        checks.Add(Pass("cache.fingerprint",
                            "Cache fingerprint matches active config."));
                    }
                    else
                    {
                        checks.Add(Warn("cache.fingerprint",
                            "Cache was generated with a different config fingerprint — may be obsolete."));
                        output.Warnings.Add("Cache fingerprint mismatch — consider re-scanning.");
                    }
                }
                catch
                {
                    checks.Add(Warn("cache.fingerprint",
                        "Could not read cache index for fingerprint check."));
                }
            }

            // 12. Validate docs structure
            ValidateFileExists(checks, "docs.agentsmd",
                Path.Combine(pathsConfig.AgentRoot, "AGENTS.md"));
            ValidateFileExists(checks, "docs.aiContract",
                Path.Combine(pathsConfig.AgentRoot, "docs", "AI_AGENT_CONTRACT.md"));

            // 13. Validate .agents structure
            ValidateDirExists(checks, "agents.rules",
                Path.Combine(pathsConfig.AgentRoot, ".agents", "rules"));
            ValidateDirExists(checks, "agents.workflows",
                Path.Combine(pathsConfig.AgentRoot, ".agents", "workflows"));
            ValidateDirExists(checks, "agents.skills",
                Path.Combine(pathsConfig.AgentRoot, ".agents", "skills"));

            output.Summary = $"Doctor completed — {checks.Count} checks performed.";
            output.Data = new { checks, fingerprint };

            if (output.Errors.Count > 0)
            {
                output.Ok = false;
                output.SafeForAutomation = false;
                output.NextRequiredAction = "fix_errors";
            }
            else if (output.Warnings.Count > 0)
            {
                output.SafeForAutomation = false;
                output.NextRequiredAction = "review_warnings";
            }
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("doctor", ex.Message);
        }

        return output;
    }

    private object CheckFileExists(string fileName)
    {
        var path = Path.Combine(_configDir, fileName);
        return File.Exists(path)
            ? Pass($"file.{fileName}", $"{fileName} exists.")
            : Fail($"file.{fileName}", $"{fileName} not found at: {path}");
    }

    /// <summary>
    /// Validate a critical safety gate. If the gate is disabled, it's an ERROR that fails the doctor.
    /// </summary>
    private static void ValidateSafetyGate(List<object> checks, JsonOutput output,
        string checkName, bool gateValue, string gateName)
    {
        if (gateValue)
        {
            checks.Add(Pass(checkName, $"{gateName} is enabled."));
        }
        else
        {
            var msg = $"Critical safety gate '{gateName}' is DISABLED. This is dangerous.";
            checks.Add(Fail(checkName, msg));
            output.Errors.Add(msg);
        }
    }

    private static void ValidatePathExists(List<object> checks, string checkName, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            checks.Add(Warn(checkName, $"{checkName}: path is not configured."));
            return;
        }

        if (Directory.Exists(path))
            checks.Add(Pass(checkName, $"{checkName} exists: {path}"));
        else
            checks.Add(Warn(checkName, $"{checkName} does not exist: {path}"));
    }

    private static void ValidateFileExists(List<object> checks, string checkName, string path)
    {
        if (File.Exists(path))
            checks.Add(Pass(checkName, $"{checkName} exists."));
        else
            checks.Add(Warn(checkName, $"{checkName} not found: {path}"));
    }

    private static void ValidateDirExists(List<object> checks, string checkName, string path)
    {
        if (Directory.Exists(path))
            checks.Add(Pass(checkName, $"{checkName} exists."));
        else
            checks.Add(Warn(checkName, $"{checkName} not found: {path}"));
    }

    private static object Pass(string check, string message) =>
        new { check, severity = "pass", message };

    private static object Warn(string check, string message) =>
        new { check, severity = "warning", message };

    private static object Fail(string check, string message) =>
        new { check, severity = "error", message };
}
