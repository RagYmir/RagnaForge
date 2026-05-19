using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Configuration;

/// <summary>
/// Represents the top-level agent configuration from ragnaforge.agent.json.
/// </summary>
public sealed class AgentConfig
{
    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("primaryOperators")]
    public List<string> PrimaryOperators { get; set; } = [];

    [JsonPropertyName("codexIntegration")]
    public bool CodexIntegration { get; set; }

    [JsonPropertyName("antigravityIntegration")]
    public bool AntigravityIntegration { get; set; }

    [JsonPropertyName("defaultOutputFormat")]
    public string DefaultOutputFormat { get; set; } = "json";

    [JsonPropertyName("cacheEnabled")]
    public bool CacheEnabled { get; set; }

    [JsonPropertyName("logsEnabled")]
    public bool LogsEnabled { get; set; }

    [JsonPropertyName("progressiveEpisodeServer")]
    public bool ProgressiveEpisodeServer { get; set; }
}
