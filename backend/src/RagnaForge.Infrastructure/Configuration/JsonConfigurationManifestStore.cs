using System.Text.Json;
using System.Text.Json.Serialization;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Infrastructure.Configuration;

public sealed class JsonConfigurationManifestStore(string workspaceRoot) : IConfigurationManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _workspaceRoot = Path.GetFullPath(workspaceRoot);
    private readonly string _manifestRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "data", "manifests"));

    public string DefaultManifestPath => Path.Combine(_manifestRoot, "repositories.local.json");

    public ConfigurationManifest Load(string path)
    {
        var fullPath = NormalizeManifestPath(path);
        var json = File.ReadAllText(fullPath);
        var manifest = JsonSerializer.Deserialize<ConfigurationManifest>(json, JsonOptions);

        return manifest ?? throw new InvalidOperationException($"Manifest could not be read: {fullPath}");
    }

    public void Save(string path, ConfigurationManifest manifest, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var fullPath = NormalizeManifestPath(path);
        if (File.Exists(fullPath) && !overwrite)
        {
            throw new InvalidOperationException($"Manifest already exists: {fullPath}. Use --force to overwrite it.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _manifestRoot);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(fullPath, json);
    }

    private string NormalizeManifestPath(string path)
    {
        var requestedPath = string.IsNullOrWhiteSpace(path) ? DefaultManifestPath : path;
        var fullPath = Path.GetFullPath(requestedPath);
        var relative = Path.GetRelativePath(_manifestRoot, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"Manifest path must stay inside {_manifestRoot}. Requested: {fullPath}");
        }

        if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Manifest path must be a .json file: {fullPath}");
        }

        return fullPath;
    }
}
