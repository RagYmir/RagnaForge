using System.Text.Json;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ReportReadTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_report_read";
    public string Description => "Read a specific operation report. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["operationId"] = new { type = "string", description = "The unique operation ID of the report." },
            ["format"] = new { type = "string", @enum = new[] { "json", "md" }, description = "Format of the report to read." }
        },
        required = new[] { "operationId" },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var operationId = GetString(arguments, "operationId");
        var format = GetString(arguments, "format") ?? "json";

        if (string.IsNullOrWhiteSpace(operationId))
            return JsonOutput.Error(Name, "operationId is required.");

        if (!OperationIdValidator.IsValid(operationId))
            return JsonOutput.Error(Name, "Invalid operationId format.");

        var output = JsonOutput.Success(Name);
        try
        {
            var reportsDir = Path.Combine(context.AgentRoot, "logs", "reports");
            var filename = $"{operationId}.report.{format}";
            var fullPath = Path.Combine(reportsDir, filename);

            // Path guard and traversal verification.
            var canonicalReports = Path.GetFullPath(reportsDir);
            var canonicalFile = Path.GetFullPath(fullPath);
            var relative = Path.GetRelativePath(canonicalReports, canonicalFile);

            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                return JsonOutput.Error(Name, "Path traversal attempt blocked.");

            if (!File.Exists(fullPath))
                return JsonOutput.Error(Name, $"Report for operation '{operationId}' in format '{format}' not found.");

            // Size Limit Check
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > 200_000) // 200KB limit
                return JsonOutput.Error(Name, "Report file is too large for MCP transmission.");

            var content = File.ReadAllText(fullPath);
            output.Summary = $"Loaded report for operation {operationId}.";
            output.Data = new
            {
                operationId,
                format,
                content
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error(Name, ex.Message);
        }

        return McpResponseLimiter.Limit(output);
    }

    private static string? GetString(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(name, out var prop) &&
        prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
}
