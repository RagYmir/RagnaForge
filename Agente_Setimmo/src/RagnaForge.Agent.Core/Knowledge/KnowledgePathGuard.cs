using System;
using System.IO;

namespace RagnaForge.Agent.Core.Knowledge;

public static class KnowledgePathGuard
{
    /// <summary>
    /// Verifies if a resolved absolute target path is located strictly within a designated safe boundary.
    /// Blocks directory traversal prefix attacks and escape attempts.
    /// </summary>
    public static bool IsWithinBoundary(string safeBoundaryRoot, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(safeBoundaryRoot) || string.IsNullOrWhiteSpace(targetPath))
            return false;

        try
        {
            var fullRoot = Path.GetFullPath(safeBoundaryRoot);
            var fullTarget = Path.GetFullPath(targetPath);
            var relative = Path.GetRelativePath(fullRoot, fullTarget);

            if (string.IsNullOrWhiteSpace(relative))
                return false;

            if (relative.Equals("..", StringComparison.Ordinal) ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
                return false;

            if (Path.IsPathRooted(relative))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Throws an exception if the target path resides outside the safe boundary root.
    /// </summary>
    public static void EnforceBoundary(string safeBoundaryRoot, string targetPath)
    {
        if (!IsWithinBoundary(safeBoundaryRoot, targetPath))
        {
            throw new UnauthorizedAccessException("Knowledge path boundary violation.");
        }
    }
}
