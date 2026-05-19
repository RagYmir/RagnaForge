using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Scanning;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Commands;

/// <summary>
/// Implements 'ragnaforge scan --project --json'.
/// Read-only scan of ragnaforgeMainProjectPath, writes cache to cache/agent/.
/// Never modifies scanned files.
/// </summary>
public sealed class ScanCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;

    public ScanCommand(string configDir, string agentRoot)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("scan");

        try
        {
            // 1. Load configs
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            // 2. Validate profile safety
            var profileIssues = PathGuard.EnsureProfileIsSafe(profile);
            if (profileIssues.Count > 0)
            {
                output.Errors.AddRange(profileIssues);
                output.Ok = false;
                output.SafeForAutomation = false;
                output.NextRequiredAction = "fix_errors";
                return output;
            }

            // 3. Validate scanRoot exists
            var scanRoot = profile.RagnaforgeMainProjectPath;
            if (string.IsNullOrWhiteSpace(scanRoot) || !Directory.Exists(scanRoot))
            {
                output = JsonOutput.Error("scan",
                    $"ragnaforgeMainProjectPath does not exist: {scanRoot ?? "(not configured)"}");
                output.ActiveProfile = pathsConfig.ActiveProfile;
                output.ConfigFingerprint = fingerprint;
                output.NextRequiredAction = "fix_errors";
                return output;
            }

            // 4. Validate PathGuard read access to scanRoot
            var guard = new PathGuard(profile.WritableRoots, profile.ReadOnlyRoots,
                safetyConfig.BlockLubEditing);

            var readCheck = guard.EnsureCanRead(scanRoot);
            if (!readCheck.IsAllowed)
            {
                output = JsonOutput.Error("scan",
                    $"PathGuard blocks read access to scanRoot: {readCheck.Reason}");
                output.ActiveProfile = pathsConfig.ActiveProfile;
                output.ConfigFingerprint = fingerprint;
                output.NextRequiredAction = "fix_errors";
                return output;
            }

            // 5. Check existing cache
            var cacheStore = new CacheStore(_agentRoot);
            var cacheExisted = cacheStore.CacheExists();
            var cacheValidation = cacheStore.Validate(
                pathsConfig.ActiveProfile, fingerprint, scanRoot);
            var cacheInvalidated = cacheExisted && !cacheValidation.IsValid;

            // 6. Scan the project
            var scanner = new ProjectScanner(guard);
            var index = scanner.Scan(scanRoot, pathsConfig.ActiveProfile, fingerprint);

            // 7. Save cache
            cacheStore.Save(index);

            // 8. Build output
            output.Summary = $"Project scan completed — {index.Stats.FilesIndexed} files indexed.";
            output.Data = new
            {
                scanType = "project",
                scanRoot = index.ScanRoot,
                filesVisited = index.Stats.FilesVisited,
                filesIndexed = index.Stats.FilesIndexed,
                filesSkipped = index.Stats.FilesSkipped,
                directoriesVisited = index.Stats.DirectoriesVisited,
                durationMs = index.Stats.DurationMs,
                cachePath = cacheStore.RelativeCachePath,
                cacheExisted,
                cacheInvalidated,
                cacheInvalidationReason = cacheInvalidated ? cacheValidation.InvalidationReason : null,
                cacheUpdated = true
            };

            if (output.Warnings.Count > 0)
            {
                output.SafeForAutomation = false;
                output.NextRequiredAction = "review_warnings";
            }
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("scan", ex.Message);
        }

        return output;
    }
}
