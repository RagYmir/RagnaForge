using System.IO;

namespace RagnaForge.Application.Assets;

public static class PathValidationHelper
{
    public static bool IsSafeLogicalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // Normalize to forward slashes for internal consistency
        var normalized = path.Replace('\\', '/');

        // Block traversal
        if (normalized.Contains("..")) return false;

        // Block absolute paths (Windows and Unix-like)
        if (normalized.Contains(':') || normalized.StartsWith('/')) return false;

        return true;
    }

    public static string NormalizePath(string path)
    {
        return path?.Replace('\\', '/') ?? string.Empty;
    }

    public static bool IsSafeCompanionPath(string primaryPath, string companionPath, string expectedExtension)
    {
        if (!IsSafeLogicalPath(primaryPath) || !IsSafeLogicalPath(companionPath)) return false;

        var normalizedPrimary = NormalizePath(primaryPath);
        var normalizedCompanion = NormalizePath(companionPath);

        // Extension check
        if (!normalizedCompanion.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)) return false;

        // Directory check: they must be in the same logical directory
        var primaryDir = Path.GetDirectoryName(normalizedPrimary)?.Replace('\\', '/') ?? string.Empty;
        var companionDir = Path.GetDirectoryName(normalizedCompanion)?.Replace('\\', '/') ?? string.Empty;

        return string.Equals(primaryDir, companionDir, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWithinBoundary(string rootPath, string targetPath)
    {
        try
        {
            var fullRoot = Path.GetFullPath(rootPath);
            var fullTarget = Path.GetFullPath(targetPath);

            // GetRelativePath returns a path relative to the root.
            // If it starts with '..', it escaped the boundary.
            var relative = Path.GetRelativePath(fullRoot, fullTarget);

            if (relative.StartsWith("..") || Path.IsPathRooted(relative))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
