using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Security;
using RagnaForge.Agent.Core.Logging;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements the 'ragnaforge status --json' command.
/// Read-only: loads configs, checks paths, reports state. Never modifies files.
/// </summary>
public sealed class StatusCommand
{
    private readonly string _configDir;

    public StatusCommand(string configDir)
    {
        _configDir = configDir;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("status");
        var pathStatuses = new List<object>();

        try
        {
            var loader = new ConfigLoader(_configDir);
            var agentConfig = loader.LoadAgentConfig();
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;
            output.Summary = $"Agente Setimmo '{agentConfig.AgentName}' — profile: {pathsConfig.ActiveProfile}";

            // Check each configured path
            var pathChecks = new Dictionary<string, string>
            {
                ["agentRoot"] = pathsConfig.AgentRoot,
                ["ragnaforgeMainProjectPath"] = profile.RagnaforgeMainProjectPath,
                ["rathenaPath"] = profile.RathenaPath,
                ["patchPath"] = profile.PatchPath,
                ["grfRepositoryPath"] = profile.GrfRepositoryPath,
                ["grfEditorPath"] = profile.GrfEditorPath
            };

            var guard = new PathGuard(profile.WritableRoots, profile.ReadOnlyRoots,
                safetyConfig.BlockLubEditing);

            foreach (var (name, path) in pathChecks)
            {
                var exists = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
                var isWritable = !string.IsNullOrWhiteSpace(path) && guard.IsInsideWritableRoot(path);
                var isReadOnly = !string.IsNullOrWhiteSpace(path) && guard.IsInsideReadOnlyRoot(path);

                pathStatuses.Add(new
                {
                    name,
                    path,
                    exists,
                    writable = isWritable,
                    readOnly = isReadOnly
                });

                if (!exists && !string.IsNullOrWhiteSpace(path))
                    output.Warnings.Add($"{name}: directory does not exist at '{path}'");
            }

            // Check GRF protection
            var grfIssues = PathGuard.EnsureGrfRepositoryIsReadOnly(profile);
            if (grfIssues.Count > 0)
            {
                output.Warnings.AddRange(grfIssues);
            }

            // Check cache directory
            var cacheDir = Path.Combine(pathsConfig.AgentRoot, "cache", "agent");
            var cacheExists = Directory.Exists(cacheDir);
            var cacheIndexPath = Path.Combine(cacheDir, "project_index.json");
            var cacheIndexExists = File.Exists(cacheIndexPath);

            // Check if cache matches current fingerprint
            var cacheMatchesProfile = false;
            if (cacheIndexExists)
            {
                try
                {
                    var cacheJson = File.ReadAllText(cacheIndexPath);
                    cacheMatchesProfile = cacheJson.Contains(fingerprint);
                }
                catch
                {
                    // Ignore read errors — cache is optional
                }
            }

            output.Data = new
            {
                agentName = agentConfig.AgentName,
                mode = agentConfig.Mode,
                primaryOperators = agentConfig.PrimaryOperators,
                activeProfile = pathsConfig.ActiveProfile,
                dbMode = profile.DbMode,
                configFingerprint = fingerprint,
                paths = pathStatuses,
                grfProtected = grfIssues.Count == 0,
                lubEditingBlocked = safetyConfig.BlockLubEditing,
                cache = new
                {
                    directory = cacheDir,
                    directoryExists = cacheExists,
                    indexExists = cacheIndexExists,
                    matchesActiveFingerprint = cacheMatchesProfile
                },
                safety = new
                {
                    safetyConfig.RequireDryRunBeforeApply,
                    safetyConfig.RequireDiffBeforeApply,
                    safetyConfig.RequireExplicitConfirmation,
                    safetyConfig.BackupBeforeApply,
                    safetyConfig.BlockOriginalGrfWrite,
                    safetyConfig.BlockLubEditing,
                    safetyConfig.InvalidateCacheOnPathChange,
                    safetyConfig.CacheMustMatchActiveProfile
                }
            };

            if (output.Warnings.Count > 0)
                output.SafeForAutomation = false;
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("status", ex.Message);
        }

        return output;
    }
}
