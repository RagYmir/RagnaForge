using System.Diagnostics;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Scanning;

/// <summary>
/// Read-only project scanner. Walks directories, collects metadata, never modifies files.
/// Respects PathGuard, ignores noise directories, skips reparse points and large files.
/// </summary>
public sealed class ProjectScanner
{
    /// <summary>
    /// Directories to ignore during scan.
    /// </summary>
    public static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".vscode",
        "bin", "obj", "node_modules",
        "dist", "build", "coverage",
        ".cache", ".vite",
        "logs", "cache", "tmp", "temp"
    };

    /// <summary>
    /// Extension-to-category mapping.
    /// </summary>
    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "dotnet", [".csproj"] = "dotnet", [".sln"] = "dotnet", [".slnx"] = "dotnet",
        [".fs"] = "dotnet", [".fsproj"] = "dotnet", [".vb"] = "dotnet", [".vbproj"] = "dotnet",
        [".razor"] = "dotnet", [".cshtml"] = "dotnet",
        [".ts"] = "frontend", [".tsx"] = "frontend", [".js"] = "frontend", [".jsx"] = "frontend",
        [".vue"] = "frontend", [".svelte"] = "frontend", [".css"] = "frontend", [".scss"] = "frontend",
        [".less"] = "frontend", [".html"] = "frontend", [".htm"] = "frontend",
        [".md"] = "docs", [".txt"] = "docs", [".rst"] = "docs",
        [".json"] = "config", [".yml"] = "config", [".yaml"] = "config",
        [".toml"] = "config", [".xml"] = "config", [".ini"] = "config",
        [".env"] = "config", [".editorconfig"] = "config",
        [".ps1"] = "script", [".bat"] = "script", [".cmd"] = "script", [".sh"] = "script",
        [".png"] = "asset", [".jpg"] = "asset", [".jpeg"] = "asset",
        [".webp"] = "asset", [".bmp"] = "asset", [".gif"] = "asset",
        [".svg"] = "asset", [".ico"] = "asset",
        [".grf"] = "asset", [".gat"] = "asset", [".rsw"] = "asset",
        [".spr"] = "asset", [".act"] = "asset", [".pal"] = "asset",
        [".lub"] = "asset", [".lua"] = "script"
    };

    private readonly PathGuard _pathGuard;
    private readonly long _maxFileSizeBytes;

    public ProjectScanner(PathGuard pathGuard, long maxFileSizeBytes = FileHasher.DefaultMaxFileSizeBytes)
    {
        _pathGuard = pathGuard;
        _maxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>
    /// Scan a directory tree read-only and return a ProjectIndex.
    /// </summary>
    public ProjectIndex Scan(string scanRoot, string activeProfile, string configFingerprint)
    {
        var normalizedRoot = PathGuard.Normalize(scanRoot);
        var sw = Stopwatch.StartNew();

        var index = new ProjectIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ActiveProfile = activeProfile,
            ConfigFingerprint = configFingerprint,
            ScanRoot = normalizedRoot
        };

        ScanDirectory(new DirectoryInfo(normalizedRoot), normalizedRoot, index);

        sw.Stop();
        index.Stats.DurationMs = sw.ElapsedMilliseconds;

        // Sort for deterministic output
        index.Entries = [.. index.Entries.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)];
        index.Skipped = [.. index.Skipped.OrderBy(s => s.Path, StringComparer.OrdinalIgnoreCase)];

        return index;
    }

    private void ScanDirectory(DirectoryInfo dir, string scanRoot, ProjectIndex index)
    {
        if (!dir.Exists) return;

        index.Stats.DirectoriesVisited++;

        // Check for reparse point on directory
        if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            index.Skipped.Add(new SkippedFileEntry
            {
                Path = GetRelativePath(dir.FullName, scanRoot),
                Reason = "reparse_point"
            });
            return;
        }

        // Process files in this directory
        FileInfo[] files;
        try
        {
            files = dir.GetFiles();
        }
        catch (UnauthorizedAccessException)
        {
            index.Skipped.Add(new SkippedFileEntry
            {
                Path = GetRelativePath(dir.FullName, scanRoot),
                Reason = "access_denied"
            });
            return;
        }
        catch (IOException)
        {
            index.Skipped.Add(new SkippedFileEntry
            {
                Path = GetRelativePath(dir.FullName, scanRoot),
                Reason = "io_error"
            });
            return;
        }

        foreach (var file in files)
        {
            index.Stats.FilesVisited++;

            // Skip reparse point files
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                index.Stats.FilesSkipped++;
                index.Skipped.Add(new SkippedFileEntry
                {
                    Path = GetRelativePath(file.FullName, scanRoot),
                    Reason = "reparse_point"
                });
                continue;
            }

            // Check PathGuard read access
            var readCheck = _pathGuard.EnsureCanRead(file.FullName);
            if (!readCheck.IsAllowed)
            {
                index.Stats.FilesSkipped++;
                index.Skipped.Add(new SkippedFileEntry
                {
                    Path = GetRelativePath(file.FullName, scanRoot),
                    Reason = "outside_allowed_root"
                });
                continue;
            }

            // Hash the file
            var hashResult = FileHasher.ComputeSha256(file.FullName, _maxFileSizeBytes);

            var entry = new ProjectIndexEntry
            {
                AbsolutePath = PathGuard.Normalize(file.FullName),
                RelativePath = GetRelativePath(file.FullName, scanRoot),
                Extension = file.Extension.ToLowerInvariant(),
                Category = ClassifyFile(file),
                SizeBytes = file.Length,
                LastWriteTimeUtc = file.LastWriteTimeUtc,
                Sha256 = hashResult.Hash,
                Included = hashResult.Success
            };

            if (!hashResult.Success)
            {
                index.Stats.FilesSkipped++;
                index.Skipped.Add(new SkippedFileEntry
                {
                    Path = entry.RelativePath,
                    Reason = hashResult.SkipReason ?? "unknown"
                });
            }
            else
            {
                index.Stats.FilesIndexed++;
            }

            index.Entries.Add(entry);
        }

        // Recurse into subdirectories
        DirectoryInfo[] subdirs;
        try
        {
            subdirs = dir.GetDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var subdir in subdirs)
        {
            if (IgnoredDirectories.Contains(subdir.Name))
            {
                index.Skipped.Add(new SkippedFileEntry
                {
                    Path = GetRelativePath(subdir.FullName, scanRoot),
                    Reason = "ignored_directory"
                });
                continue;
            }

            ScanDirectory(subdir, scanRoot, index);
        }
    }

    /// <summary>
    /// Classify a file into a simple category based on extension and path.
    /// </summary>
    public static string ClassifyFile(FileInfo file)
    {
        // Check if inside a test directory
        var dirName = file.Directory?.Name ?? string.Empty;
        if (dirName.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            dirName.Equals("test", StringComparison.OrdinalIgnoreCase) ||
            file.FullName.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase) ||
            file.FullName.Contains($"{Path.DirectorySeparatorChar}test{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
        {
            return "test";
        }

        // Check known file names
        var fileName = file.Name;
        if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase))
        {
            return "frontend";
        }

        if (fileName.StartsWith("vite.config", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("webpack.config", StringComparison.OrdinalIgnoreCase))
        {
            return "frontend";
        }

        // Extension-based classification
        var ext = file.Extension.ToLowerInvariant();
        return CategoryMap.GetValueOrDefault(ext, "unknown");
    }

    /// <summary>
    /// Safely enumerate all files in a directory tree, respecting ignored directories and reparse points.
    /// Does NOT compute hashes.
    /// </summary>
    public static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        var dir = new DirectoryInfo(root);
        if (!dir.Exists) yield break;

        var queue = new Queue<DirectoryInfo>();
        queue.Enqueue(dir);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (IgnoredDirectories.Contains(current.Name)) continue;

            FileInfo[] files;
            try { files = current.GetFiles(); }
            catch { continue; }

            foreach (var file in files)
            {
                if (file.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                yield return file.FullName;
            }

            DirectoryInfo[] subdirs;
            try { subdirs = current.GetDirectories(); }
            catch { continue; }

            foreach (var subdir in subdirs) queue.Enqueue(subdir);
        }
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        return Path.GetRelativePath(basePath, fullPath);
    }
}
