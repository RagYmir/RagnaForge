using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Scanning;

/// <summary>
/// Full project index stored in cache/agent/project_index.json.
/// </summary>
public sealed class ProjectIndex
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("generatedAtUtc")]
    public DateTimeOffset GeneratedAtUtc { get; set; }

    [JsonPropertyName("agentVersion")]
    public string AgentVersion { get; set; } = RagnaForge.Agent.Core.AgentVersion.Current;

    [JsonPropertyName("activeProfile")]
    public string ActiveProfile { get; set; } = string.Empty;

    [JsonPropertyName("configFingerprint")]
    public string ConfigFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("scanType")]
    public string ScanType { get; set; } = "project";

    [JsonPropertyName("scanRoot")]
    public string ScanRoot { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public ScanStats Stats { get; set; } = new();

    [JsonPropertyName("skipped")]
    public List<SkippedFileEntry> Skipped { get; set; } = [];

    [JsonPropertyName("entries")]
    public List<ProjectIndexEntry> Entries { get; set; } = [];
}

/// <summary>
/// Scan statistics.
/// </summary>
public sealed class ScanStats
{
    [JsonPropertyName("filesVisited")]
    public int FilesVisited { get; set; }

    [JsonPropertyName("filesIndexed")]
    public int FilesIndexed { get; set; }

    [JsonPropertyName("filesSkipped")]
    public int FilesSkipped { get; set; }

    [JsonPropertyName("directoriesVisited")]
    public int DirectoriesVisited { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }
}

/// <summary>
/// A single indexed file entry.
/// </summary>
public sealed class ProjectIndexEntry
{
    [JsonPropertyName("absolutePath")]
    public string AbsolutePath { get; set; } = string.Empty;

    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("extension")]
    public string Extension { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "unknown";

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("lastWriteTimeUtc")]
    public DateTimeOffset LastWriteTimeUtc { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("included")]
    public bool Included { get; set; } = true;
}

/// <summary>
/// A skipped file or directory.
/// </summary>
public sealed class SkippedFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
