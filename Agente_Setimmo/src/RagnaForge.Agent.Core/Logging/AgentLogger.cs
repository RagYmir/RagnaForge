using System.Text.Json;

namespace RagnaForge.Agent.Core.Logging;

/// <summary>
/// Structured logger that writes JSON log entries to files inside the agent's logs directory.
/// Does not log API keys, secrets, or massive content.
/// Only allows known categories to prevent path traversal via category names.
/// </summary>
public sealed class AgentLogger
{
    private readonly string _logsDir;

    /// <summary>
    /// The only allowed log categories. Any other category is rejected.
    /// </summary>
    public static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "agent",
        "operations",
        "validations",
        "diffs",
        "rollbacks",
        "reports"
    };

    public AgentLogger(string agentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentRoot);
        _logsDir = Path.Combine(agentRoot, "logs");
    }

    /// <summary>
    /// Validate that a category is safe to use.
    /// Blocks unknown categories, path traversal (..), forward slashes and backslashes.
    /// </summary>
    public static bool IsCategorySafe(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        // Block traversal and path separators
        if (category.Contains("..") || category.Contains('/') || category.Contains('\\'))
            return false;

        // Must be a known category
        return AllowedCategories.Contains(category);
    }

    /// <summary>
    /// Log an operation entry to the specified subdirectory.
    /// Only accepts known safe categories.
    /// </summary>
    public void Log(string category, object entry)
    {
        if (!IsCategorySafe(category))
            throw new ArgumentException(
                $"Invalid log category '{category}'. Allowed: {string.Join(", ", AllowedCategories)}.",
                nameof(category));

        var categoryDir = Path.Combine(_logsDir, category);

        // Ensure the resolved path stays inside logs/
        var normalizedDir = Path.GetFullPath(categoryDir);
        var normalizedLogs = Path.GetFullPath(_logsDir);
        if (!normalizedDir.StartsWith(normalizedLogs, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Log path '{normalizedDir}' escapes the logs directory '{normalizedLogs}'.");

        Directory.CreateDirectory(categoryDir);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        var fileName = $"{timestamp}.json";
        var filePath = Path.Combine(categoryDir, fileName);

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Log to the 'agent' category.
    /// </summary>
    public void LogAgent(string operationId, string activeProfile,
        string configFingerprint, string operation, string result,
        List<string>? warnings = null, List<string>? errors = null)
    {
        Log("agent", new
        {
            operationId,
            activeProfile,
            configFingerprint,
            timestamp = DateTimeOffset.UtcNow,
            operation,
            result,
            warnings = warnings ?? [],
            errors = errors ?? []
        });
    }

    /// <summary>
    /// Ensure all log subdirectories exist.
    /// </summary>
    public void EnsureDirectories()
    {
        foreach (var sub in AllowedCategories)
        {
            Directory.CreateDirectory(Path.Combine(_logsDir, sub));
        }
    }
}
