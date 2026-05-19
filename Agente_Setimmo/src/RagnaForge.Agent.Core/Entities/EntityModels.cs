using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Entities;

/// <summary>
/// A parsed item entry from rAthena item_db YAML.
/// </summary>
public sealed class ItemEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("aegisName")] public string AegisName { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; set; } = "server";
    [JsonPropertyName("dbMode")] public string DbMode { get; set; } = "unknown";
    [JsonPropertyName("sourceFile")] public string SourceFile { get; set; } = string.Empty;
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = string.Empty;
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "high";
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// A parsed NPC entry from rAthena script files.
/// </summary>
public sealed class NpcEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty; // script, shop, warp
    [JsonPropertyName("map")] public string Map { get; set; } = string.Empty;
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("direction")] public int Direction { get; set; }
    [JsonPropertyName("sprite")] public string Sprite { get; set; } = string.Empty;
    [JsonPropertyName("sourceFile")] public string SourceFile { get; set; } = string.Empty;
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = string.Empty;
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "high";
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// A parsed monster entry from rAthena mob_db YAML.
/// </summary>
public sealed class MonsterEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("aegisName")] public string AegisName { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("dbMode")] public string DbMode { get; set; } = "unknown";
    [JsonPropertyName("sourceFile")] public string SourceFile { get; set; } = string.Empty;
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = string.Empty;
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "high";
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// A parsed map entry consolidated from server and client sources.
/// </summary>
public sealed class MapEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = "server"; // server, client, both
    [JsonPropertyName("sourceFile")] public string SourceFile { get; set; } = string.Empty;
    [JsonPropertyName("relativePath")] public string RelativePath { get; set; } = string.Empty;
    [JsonPropertyName("hasRsw")] public bool HasRsw { get; set; }
    [JsonPropertyName("hasGnd")] public bool HasGnd { get; set; }
    [JsonPropertyName("hasGat")] public bool HasGat { get; set; }
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "high";
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Unified entity index stored in cache.
/// </summary>
public sealed class EntityIndex
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("generatedAtUtc")] public DateTimeOffset GeneratedAtUtc { get; set; }
    [JsonPropertyName("agentVersion")] public string AgentVersion { get; set; } = RagnaForge.Agent.Core.AgentVersion.Current;
    [JsonPropertyName("activeProfile")] public string ActiveProfile { get; set; } = string.Empty;
    [JsonPropertyName("configFingerprint")] public string ConfigFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("sourcePaths")] public List<string> SourcePaths { get; set; } = [];
    [JsonPropertyName("stats")] public EntityIndexStats Stats { get; set; } = new();
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
    [JsonPropertyName("items")] public List<ItemEntry> Items { get; set; } = [];
    [JsonPropertyName("npcs")] public List<NpcEntry> Npcs { get; set; } = [];
    [JsonPropertyName("monsters")] public List<MonsterEntry> Monsters { get; set; } = [];
    [JsonPropertyName("maps")] public List<MapEntry> Maps { get; set; } = [];
}

public sealed class EntityIndexStats
{
    [JsonPropertyName("itemsFound")] public int ItemsFound { get; set; }
    [JsonPropertyName("npcsFound")] public int NpcsFound { get; set; }
    [JsonPropertyName("monstersFound")] public int MonstersFound { get; set; }
    [JsonPropertyName("mapsFound")] public int MapsFound { get; set; }
    [JsonPropertyName("filesScanned")] public int FilesScanned { get; set; }
    [JsonPropertyName("filesParsed")] public int FilesParsed { get; set; }
    [JsonPropertyName("filesSkipped")] public int FilesSkipped { get; set; }
    [JsonPropertyName("durationMs")] public long DurationMs { get; set; }
    [JsonPropertyName("warnings")] public int WarningCount { get; set; }
}

/// <summary>
/// A validation issue found during entity analysis.
/// </summary>
public sealed class ValidationIssue
{
    [JsonPropertyName("severity")] public string Severity { get; set; } = "warning";
    [JsonPropertyName("scope")] public string Scope { get; set; } = "external-data";
    [JsonPropertyName("blockingFor")] public List<string> BlockingFor { get; set; } = [];
    [JsonPropertyName("notBlockingFor")] public List<string> NotBlockingFor { get; set; } = [];
    [JsonPropertyName("safeForCurrentTask")] public bool SafeForCurrentTask { get; set; } = true;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("entityType")] public string EntityType { get; set; } = string.Empty;
    [JsonPropertyName("entityId")] public string? EntityId { get; set; }
    [JsonPropertyName("entityName")] public string? EntityName { get; set; }
    [JsonPropertyName("sourceFile")] public string? SourceFile { get; set; }
    [JsonPropertyName("line")] public int? Line { get; set; }
    [JsonPropertyName("recommendation")] public string Recommendation { get; set; } = string.Empty;
    [JsonPropertyName("knowledgeHints")] public List<string> KnowledgeHints { get; set; } = [];
    [JsonPropertyName("recommendedKnowledgeEntryIds")] public List<string> RecommendedKnowledgeEntryIds { get; set; } = [];
}

/// <summary>
/// An operation manifest for dry-run/diff/report.
/// </summary>
public sealed class OperationManifest
{
    [JsonPropertyName("operationId")] public string OperationId { get; set; } = string.Empty;
    [JsonPropertyName("createdAtUtc")] public DateTimeOffset CreatedAtUtc { get; set; }
    [JsonPropertyName("agentVersion")] public string AgentVersion { get; set; } = RagnaForge.Agent.Core.AgentVersion.Current;
    [JsonPropertyName("activeProfile")] public string ActiveProfile { get; set; } = string.Empty;
    [JsonPropertyName("configFingerprint")] public string ConfigFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("entityType")] public string EntityType { get; set; } = string.Empty;
    [JsonPropertyName("operationType")] public string OperationType { get; set; } = "dry-run";
    [JsonPropertyName("input")] public object? Input { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "planned";
    [JsonPropertyName("affectedFiles")] public List<AffectedFile> AffectedFiles { get; set; } = [];
    [JsonPropertyName("validationIssues")] public List<ValidationIssue> ValidationIssues { get; set; } = [];
    [JsonPropertyName("warnings")] public List<string> Warnings { get; set; } = [];
    [JsonPropertyName("errors")] public List<string> Errors { get; set; } = [];
    [JsonPropertyName("applied")] public bool Applied { get; set; }
    [JsonPropertyName("diffPath")] public string? DiffPath { get; set; }
    [JsonPropertyName("rollbackPlanPath")] public string? RollbackPlanPath { get; set; }
}

public sealed class AffectedFile
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("action")] public string Action { get; set; } = string.Empty; // create, modify, append
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("diffPreview")] public string? DiffPreview { get; set; }
}
