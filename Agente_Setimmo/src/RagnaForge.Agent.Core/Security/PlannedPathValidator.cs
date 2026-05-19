using RagnaForge.Agent.Core.Configuration;

namespace RagnaForge.Agent.Core.Security;

/// <summary>
/// Validates paths planned during dry-run to ensure safety before saving manifests.
/// </summary>
public static class PlannedPathValidator
{
    /// <summary>
    /// Validates a planned file path against safety rules.
    /// Blocks traversal, illegal extensions, and ensures it stays within writable roots.
    /// </summary>
    public static List<string> Validate(string path, ProfileConfig profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add("Planned path cannot be empty.");
            return errors;
        }

        // 1. Block Traversal
        if (path.Contains("..") || path.Contains("\\..") || path.Contains("/.."))
            errors.Add($"Security violation: Path traversal detected in planned path: {path}");

        // 2. Block .lub editing
        if (path.EndsWith(".lub", StringComparison.OrdinalIgnoreCase))
            errors.Add("Security violation: Editing .lub files is strictly blocked by policy.");

        // 3. Block illegal characters in entity names that become filenames
        var fileName = Path.GetFileName(path);
        if (fileName.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
            errors.Add($"Invalid characters in planned filename: {fileName}");

        // 4. Ensure it is within writable roots and not in read-only roots
        var guard = new PathGuard(profile.WritableRoots, profile.ReadOnlyRoots);
        var result = guard.EnsureCanWrite(path);
        if (!result.IsAllowed)
            errors.Add(result.Reason ?? "Path is not allowed by policy.");

        return errors;
    }
}
