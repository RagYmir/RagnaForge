using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Abstractions;

public interface IConfigurationManifestStore
{
    string DefaultManifestPath { get; }

    ConfigurationManifest Load(string path);

    void Save(string path, ConfigurationManifest manifest, bool overwrite);
}
