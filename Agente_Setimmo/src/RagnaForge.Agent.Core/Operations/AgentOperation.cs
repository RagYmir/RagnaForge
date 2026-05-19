using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Operations;

/// <summary>
/// Standardized model for all agent operations.
/// Every CLI command produces an AgentOperation as its result.
/// </summary>
public sealed class AgentOperation
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("activeProfile")]
    public string ActiveProfile { get; set; } = string.Empty;

    [JsonPropertyName("configFingerprint")]
    public string ConfigFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("filesScanned")]
    public int? FilesScanned { get; set; }

    [JsonPropertyName("filesToChange")]
    public int? FilesToChange { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("diffPath")]
    public string? DiffPath { get; set; }

    [JsonPropertyName("rollbackPlanPath")]
    public string? RollbackPlanPath { get; set; }

    [JsonPropertyName("validationStatus")]
    public string? ValidationStatus { get; set; }

    [JsonPropertyName("requiresConfirmation")]
    public bool RequiresConfirmation { get; set; }

    [JsonPropertyName("confirmationText")]
    public string? ConfirmationText { get; set; }

    [JsonPropertyName("nextRequiredAction")]
    public string NextRequiredAction { get; set; } = "none";

    [JsonPropertyName("aiOperator")]
    public string? AiOperator { get; set; }
}
