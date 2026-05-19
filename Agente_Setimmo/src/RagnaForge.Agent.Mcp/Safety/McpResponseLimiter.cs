using System.Text.Json;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Mcp.Safety;

public static class McpResponseLimiter
{
    public const int DefaultMaxJsonChars = 60_000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonOutput Limit(JsonOutput output, int maxJsonChars = DefaultMaxJsonChars)
    {
        var json = JsonSerializer.Serialize(output, JsonOpts);
        if (json.Length <= maxJsonChars) return output;

        return new JsonOutput
        {
            Ok = output.Ok,
            Mode = output.Mode,
            ActiveProfile = output.ActiveProfile,
            ConfigFingerprint = output.ConfigFingerprint,
            Summary = $"{output.Summary ?? "Response"} Response truncated for MCP safety.",
            Warnings = [.. output.Warnings, $"MCP response exceeded {maxJsonChars} characters and was truncated."],
            Errors = output.Errors,
            NextRequiredAction = output.NextRequiredAction,
            SafeForAutomation = false,
            OperationId = output.OperationId,
            Data = new
            {
                truncated = true,
                originalLength = json.Length,
                maxJsonChars,
                output.Mode,
                output.Ok
            }
        };
    }
}
