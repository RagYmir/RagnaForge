using System.Text.Json;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;
using RagnaForge.Agent.Mcp.Tools;

namespace RagnaForge.Agent.Mcp.Resources;

public sealed class McpResourceRegistry
{
    private const long MaxResourceBytes = 200_000;
    private readonly McpToolRegistry _tools;
    private readonly string _agentRoot;

    public McpResourceRegistry(McpToolRegistry tools, string agentRoot)
    {
        _tools = tools;
        _agentRoot = Path.GetFullPath(agentRoot);
    }

    public IReadOnlyList<object> ListResources() =>
    [
        Resource("ragnaforge://status", "RagnaForge status", "Current read-only status summary.", "application/json"),
        Resource("ragnaforge://safety", "RagnaForge safety", "Safety policy and blocked write operations.", "application/json"),
        Resource("ragnaforge://docs/readme", "README", "Agent README.", "text/markdown"),
        Resource("ragnaforge://docs/safety", "Safety docs", "Agent safety documentation.", "text/markdown"),
        Resource("ragnaforge://docs/mcp", "MCP docs", "Agent MCP documentation.", "text/markdown"),
        Resource("ragnaforge://reports", "Reports", "List of local operation reports.", "application/json"),
        Resource("ragnaforge://reports/{id}", "Report by id", "Read a small local report by safe id.", "text/markdown"),
        Resource("ragnaforge://inputs/dry-run", "Dry-run inputs", "List of MCP dry-run input files under agentRoot.", "application/json")
    ];

    public object ReadResource(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return ErrorResource(uri ?? string.Empty, "resources/read requires params.uri.");

        if (ContainsUnsafePathText(uri))
            return ErrorResource(uri, "Safety policy violation: absolute paths and traversal are blocked.");

        return uri switch
        {
            "ragnaforge://status" => JsonResource(uri, _tools.Execute("ragnaforge_status", EmptyArgs())),
            "ragnaforge://safety" => JsonResource(uri, _tools.Execute("ragnaforge_security_policy", EmptyArgs())),
            "ragnaforge://docs/readme" => FileResource(uri, Path.Combine(_agentRoot, "README.md"), "text/markdown"),
            "ragnaforge://docs/safety" => FileResource(uri, Path.Combine(_agentRoot, "docs", "SAFETY.md"), "text/markdown"),
            "ragnaforge://docs/mcp" => FileResource(uri, Path.Combine(_agentRoot, "docs", "MCP.md"), "text/markdown"),
            "ragnaforge://reports" => JsonResource(uri, ListReports()),
            "ragnaforge://inputs/dry-run" => JsonResource(uri, ListDryRunInputs()),
            _ when uri.StartsWith("ragnaforge://reports/", StringComparison.OrdinalIgnoreCase) =>
                ReportResource(uri, uri["ragnaforge://reports/".Length..]),
            _ => ErrorResource(uri, "Unknown RagnaForge resource.")
        };
    }

    private object ReportResource(string uri, string id)
    {
        if (!IsSafeId(id))
            return ErrorResource(uri, "Invalid report id.");

        var reportsDir = Path.Combine(_agentRoot, "logs", "reports");
        var candidates = id.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                         id.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? [id]
            : new[] { $"{id}.report.md", $"{id}.report.json", $"{id}.md", $"{id}.json" };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.Combine(reportsDir, candidate);
            if (!IsInside(fullPath, reportsDir))
                return ErrorResource(uri, "Path traversal attempt blocked.");

            if (File.Exists(fullPath))
                return FileResource(uri, fullPath, Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? "application/json"
                    : "text/markdown");
        }

        return ErrorResource(uri, "Report not found.");
    }

    private object FileResource(string uri, string path, string mimeType)
    {
        if (!IsInside(path, _agentRoot))
            return ErrorResource(uri, "Resource path must stay inside agentRoot.");

        if (!File.Exists(path))
            return ErrorResource(uri, "Resource file not found.");

        var info = new FileInfo(path);
        if (info.Length > MaxResourceBytes)
            return ErrorResource(uri, "Resource is too large for MCP transmission.");

        return Contents(uri, mimeType, File.ReadAllText(path));
    }

    private object ListReports()
    {
        var reportsDir = Path.Combine(_agentRoot, "logs", "reports");
        if (!Directory.Exists(reportsDir))
            return new { reports = Array.Empty<object>() };

        var reports = Directory.GetFiles(reportsDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(100)
            .Select(path => new
            {
                id = Path.GetFileName(path),
                sizeBytes = new FileInfo(path).Length,
                modifiedAtUtc = File.GetLastWriteTimeUtc(path)
            })
            .ToArray();

        return new { reports };
    }

    private object ListDryRunInputs()
    {
        var inputDir = Path.Combine(_agentRoot, "inputs", "dry-run");
        if (!Directory.Exists(inputDir))
            return new { inputs = Array.Empty<object>() };

        var inputs = Directory.GetFiles(inputDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(100)
            .Select(path => new
            {
                id = Path.GetFileName(path),
                sizeBytes = new FileInfo(path).Length,
                modifiedAtUtc = File.GetLastWriteTimeUtc(path)
            })
            .ToArray();

        return new { inputs };
    }

    private static object Resource(string uri, string name, string description, string mimeType) => new
    {
        uri,
        name,
        description,
        mimeType
    };

    private static object JsonResource(string uri, object value) =>
        Contents(uri, "application/json", JsonSerializer.Serialize(value, JsonOpts));

    private static object Contents(string uri, string mimeType, string text) => new
    {
        contents = new[]
        {
            new
            {
                uri,
                mimeType,
                text
            }
        }
    };

    private static object ErrorResource(string uri, string message) => new
    {
        contents = new[]
        {
            new
            {
                uri,
                mimeType = "application/json",
                text = JsonSerializer.Serialize(new
                {
                    ok = false,
                    readOnly = true,
                    correlationId = JsonOutput.GenerateOperationId(),
                    errors = new[] { message }
                }, JsonOpts)
            }
        }
    };

    private static JsonElement EmptyArgs() => JsonDocument.Parse("{}").RootElement;

    private static bool ContainsUnsafePathText(string text) =>
        text.Contains("..", StringComparison.Ordinal) ||
        text.Contains(":\\", StringComparison.Ordinal) ||
        text.Contains(":/", StringComparison.Ordinal) ||
        text.Contains('\\', StringComparison.Ordinal);

    private static bool IsSafeId(string id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Length <= 120 &&
        id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.');

    private static bool IsInside(string path, string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
