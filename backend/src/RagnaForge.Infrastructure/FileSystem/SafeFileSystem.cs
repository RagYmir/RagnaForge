using RagnaForge.Domain.Discovery;

namespace RagnaForge.Infrastructure.FileSystem;

internal static class SafeFileSystem
{
    public static bool DirectoryExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    public static bool FileExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public static string Combine(string root, params string[] segments)
    {
        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
        }

        return current;
    }

    public static IReadOnlyList<string> ReadLinesIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return File.ReadLines(path).ToArray();
    }

    public static IEnumerable<FileInfo> EnumerateFiles(
        string root,
        string searchPattern,
        SearchOption searchOption)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        IEnumerator<FileInfo>? enumerator = null;
        try
        {
            enumerator = new DirectoryInfo(root)
                .EnumerateFiles(searchPattern, searchOption)
                .GetEnumerator();

            while (true)
            {
                FileInfo current;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }

                    current = enumerator.Current;
                }
                catch (UnauthorizedAccessException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }

                yield return current;
            }
        }
        finally
        {
            enumerator?.Dispose();
        }
    }

    public static FileSnapshot Snapshot(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return new FileSnapshot(fullPath, Path.GetFileName(fullPath), false, 0, null);
        }

        var file = new FileInfo(fullPath);
        return new FileSnapshot(
            file.FullName,
            file.Name,
            true,
            file.Length,
            new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero));
    }
}

