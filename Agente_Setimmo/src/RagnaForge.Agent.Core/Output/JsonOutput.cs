using System.Text.Json;
using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Output;

/// <summary>
/// Standard JSON output for all CLI commands.
/// Designed for consumption by Codex, Antigravity and other AI operators.
/// Every output always has a non-empty operationId for log correlation.
/// </summary>
public sealed class JsonOutput
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = GenerateOperationId();

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("activeProfile")]
    public string? ActiveProfile { get; set; }

    [JsonPropertyName("configFingerprint")]
    public string? ConfigFingerprint { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("nextRequiredAction")]
    public string NextRequiredAction { get; set; } = "none";

    [JsonPropertyName("safeForAutomation")]
    public bool SafeForAutomation { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Create a success output. Always generates a unique operationId.
    /// </summary>
    public static JsonOutput Success(string mode, string? summary = null) =>
        new() { Ok = true, Mode = mode, Summary = summary, SafeForAutomation = true };

    /// <summary>
    /// Create an error output. Always generates a unique operationId.
    /// </summary>
    public static JsonOutput Error(string mode, string error) =>
        new() { Ok = false, Mode = mode, Errors = [error], SafeForAutomation = false };

    /// <summary>
    /// Create an error output from multiple errors. Always generates a unique operationId.
    /// </summary>
    public static JsonOutput Error(string mode, List<string> errors) =>
        new() { Ok = false, Mode = mode, Errors = errors, SafeForAutomation = false };

    /// <summary>
    /// Create a fatal error output for unhandled exceptions.
    /// </summary>
    public static JsonOutput Fatal(string error) =>
        new()
        {
            Ok = false,
            Mode = "fatal",
            Errors = [error],
            SafeForAutomation = false,
            NextRequiredAction = "fix_errors"
        };

    /// <summary>
    /// Serialize to JSON string.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Generate a short unique operation ID.
    /// </summary>
    public static string GenerateOperationId() => Guid.NewGuid().ToString("N")[..12];
}
