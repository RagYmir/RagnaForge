using System.Text.Json;
using System.Text.Json.Serialization;
using RagnaForge.Domain.Visuals;

namespace RagnaForge.Infrastructure.Visuals;

public sealed class JsonVisualThemeManifestStore(string workspaceRoot)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _manifestRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "data", "manifests"));

    public string DefaultManifestPath => Path.Combine(_manifestRoot, "visual-equipment-themes.local.json");

    public VisualThemeManifest Load(string path)
    {
        var fullPath = NormalizeManifestPath(path);
        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<VisualThemeManifest>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Visual theme manifest could not be read: {fullPath}");
    }

    public void Save(string path, VisualThemeManifest manifest, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var fullPath = NormalizeManifestPath(path);
        if (File.Exists(fullPath) && !overwrite)
        {
            throw new InvalidOperationException($"Visual theme manifest already exists: {fullPath}. Use --force to overwrite it.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _manifestRoot);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private string NormalizeManifestPath(string path)
    {
        var requestedPath = string.IsNullOrWhiteSpace(path) ? DefaultManifestPath : path;
        var fullPath = Path.GetFullPath(requestedPath);
        var relative = Path.GetRelativePath(_manifestRoot, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"Visual theme manifest path must stay inside {_manifestRoot}. Requested: {fullPath}");
        }

        if (!fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Visual theme manifest path must be a .json file: {fullPath}");
        }

        return fullPath;
    }
}
