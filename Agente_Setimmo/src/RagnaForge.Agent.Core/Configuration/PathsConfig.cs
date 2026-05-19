using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Configuration;

/// <summary>
/// Top-level paths configuration from paths.json.
/// Contains agentRoot, activeProfile name and all profile definitions.
/// </summary>
public sealed class PathsConfig
{
    [JsonPropertyName("agentRoot")]
    public string AgentRoot { get; set; } = string.Empty;

    [JsonPropertyName("activeProfile")]
    public string ActiveProfile { get; set; } = string.Empty;

    [JsonPropertyName("profiles")]
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new();
}

/// <summary>
/// A single profile definition with all configurable paths.
/// Never hardcode these values — always load from config.
/// </summary>
public sealed class ProfileConfig
{
    [JsonPropertyName("ragnaforgeMainProjectPath")]
    public string RagnaforgeMainProjectPath { get; set; } = string.Empty;

    [JsonPropertyName("rathenaPath")]
    public string RathenaPath { get; set; } = string.Empty;

    [JsonPropertyName("patchPath")]
    public string PatchPath { get; set; } = string.Empty;

    [JsonPropertyName("grfRepositoryPath")]
    public string GrfRepositoryPath { get; set; } = string.Empty;

    [JsonPropertyName("grfEditorPath")]
    public string GrfEditorPath { get; set; } = string.Empty;

    [JsonPropertyName("dbMode")]
    public string DbMode { get; set; } = "renewal";

    [JsonPropertyName("writableRoots")]
    public List<string> WritableRoots { get; set; } = [];

    [JsonPropertyName("readOnlyRoots")]
    public List<string> ReadOnlyRoots { get; set; } = [];
}
