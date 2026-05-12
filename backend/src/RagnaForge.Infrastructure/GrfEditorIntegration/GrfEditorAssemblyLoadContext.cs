using System.Reflection;
using System.Runtime.Loader;

namespace RagnaForge.Infrastructure.GrfEditorIntegration;

internal sealed class GrfEditorAssemblyLoadContext(string hostExecutablePath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly string _hostExecutablePath = hostExecutablePath;
    private readonly Dictionary<string, string> _resourceMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private Assembly? _hostAssembly;

    public Assembly LoadHostAssembly()
    {
        _hostAssembly ??= LoadFromAssemblyPath(_hostExecutablePath);
        MapResources();
        return _hostAssembly;
    }

    public Assembly LoadAssembly(string simpleName)
    {
        LoadHostAssembly();
        return LoadFromAssemblyName(new AssemblyName(simpleName));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
        {
            return null;
        }

        if (_loadedAssemblies.TryGetValue(assemblyName.Name, out var loaded))
        {
            return loaded;
        }

        LoadHostAssembly();
        if (!_resourceMap.TryGetValue(assemblyName.Name, out var resourceName) || _hostAssembly is null)
        {
            return null;
        }

        using var stream = _hostAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        loaded = LoadFromStream(memory);
        _loadedAssemblies[assemblyName.Name] = loaded;
        return loaded;
    }

    private void MapResources()
    {
        if (_hostAssembly is null || _resourceMap.Count > 0)
        {
            return;
        }

        foreach (var resourceName in _hostAssembly.GetManifestResourceNames()
                     .Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var marker = resourceName.LastIndexOf(".Files.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                continue;
            }

            var simpleName = resourceName[(marker + 7)..^4];
            if (!_resourceMap.ContainsKey(simpleName))
            {
                _resourceMap.Add(simpleName, resourceName);
            }
        }
    }
}
