using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;

namespace RagnaForge.Infrastructure.Grf;

public sealed class JsonGrfContainerIndexStore(string workspaceRoot) : IGrfContainerIndexStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _indexRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "data", "indexes"));

    public string BuildDefaultIndexPath(string containerPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(containerPath);
        var safeName = new string(fileName
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "grf-container";
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(containerPath))))[..12];
        return Path.Combine(_indexRoot, $"{safeName}-{hash}.index.json");
    }

    public GrfContainerContentIndexDocument? TryLoad(string path)
    {
        var fullPath = NormalizeIndexPath(path);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<GrfContainerContentIndexDocument>(json, JsonOptions);
    }

    public void Save(string path, GrfContainerContentIndexDocument document, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fullPath = NormalizeIndexPath(path);
        if (File.Exists(fullPath) && !overwrite)
        {
            throw new InvalidOperationException($"GRF content index already exists: {fullPath}. Use --force to overwrite it.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _indexRoot);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private string NormalizeIndexPath(string path)
    {
        var requestedPath = string.IsNullOrWhiteSpace(path) ? BuildDefaultIndexPath("default.grf") : path;
        var fullPath = Path.GetFullPath(requestedPath);
        var relative = Path.GetRelativePath(_indexRoot, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"GRF content index path must stay inside {_indexRoot}. Requested: {fullPath}");
        }

        if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"GRF content index path must be a .json file: {fullPath}");
        }

        return fullPath;
    }
}
