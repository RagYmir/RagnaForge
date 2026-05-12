using System.Text.Json;
using System.Text.Json.Serialization;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;

namespace RagnaForge.Infrastructure.Grf;

public sealed class JsonGrfRepositoryIndexStore(string workspaceRoot) : IGrfRepositoryIndexStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _cacheRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "data", "cache"));

    public string DefaultIndexPath => Path.Combine(_cacheRoot, "grf-repository.index.json");

    public GrfRepositoryIndexDocument? TryLoad(string path)
    {
        var fullPath = NormalizeIndexPath(path);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var json = File.ReadAllText(fullPath);
        var document = JsonSerializer.Deserialize<GrfRepositoryIndexDocument>(json, JsonOptions);
        if (document?.SchemaVersion != GrfRepositoryIndexDocument.CurrentSchemaVersion)
        {
            return null;
        }

        return document;
    }

    public void Save(string path, GrfRepositoryIndexDocument document, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fullPath = NormalizeIndexPath(path);
        if (File.Exists(fullPath) && !overwrite)
        {
            throw new InvalidOperationException($"GRF index already exists: {fullPath}. Use --force to overwrite it.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _cacheRoot);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private string NormalizeIndexPath(string path)
    {
        var requestedPath = string.IsNullOrWhiteSpace(path) ? DefaultIndexPath : path;
        var fullPath = Path.GetFullPath(requestedPath);
        var relative = Path.GetRelativePath(_cacheRoot, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"GRF index path must stay inside {_cacheRoot}. Requested: {fullPath}");
        }

        if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"GRF index path must be a .json file: {fullPath}");
        }

        return fullPath;
    }
}
