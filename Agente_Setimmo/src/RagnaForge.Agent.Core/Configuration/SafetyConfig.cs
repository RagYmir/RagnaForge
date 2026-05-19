using System.Text.Json.Serialization;

namespace RagnaForge.Agent.Core.Configuration;

/// <summary>
/// Safety configuration from safety.json.
/// Enforces mandatory gates before any destructive operation.
/// </summary>
public sealed class SafetyConfig
{
    [JsonPropertyName("requireDryRunBeforeApply")]
    public bool RequireDryRunBeforeApply { get; set; } = true;

    [JsonPropertyName("requireDiffBeforeApply")]
    public bool RequireDiffBeforeApply { get; set; } = true;

    [JsonPropertyName("requireValidationBeforeApply")]
    public bool RequireValidationBeforeApply { get; set; } = true;

    [JsonPropertyName("requireExplicitConfirmation")]
    public bool RequireExplicitConfirmation { get; set; } = true;

    [JsonPropertyName("applyConfirmationText")]
    public string ApplyConfirmationText { get; set; } = "APLICAR";

    [JsonPropertyName("rollbackConfirmationText")]
    public string RollbackConfirmationText { get; set; } = "REVERTER";

    [JsonPropertyName("backupBeforeApply")]
    public bool BackupBeforeApply { get; set; } = true;

    [JsonPropertyName("blockOriginalGrfWrite")]
    public bool BlockOriginalGrfWrite { get; set; } = true;

    [JsonPropertyName("blockLubEditing")]
    public bool BlockLubEditing { get; set; } = true;

    [JsonPropertyName("maxFilesPerOperationWithoutWarning")]
    public int MaxFilesPerOperationWithoutWarning { get; set; } = 10;

    [JsonPropertyName("stopOnCriticalWarning")]
    public bool StopOnCriticalWarning { get; set; } = true;

    [JsonPropertyName("aiOperatorsMustUseReviewMode")]
    public bool AiOperatorsMustUseReviewMode { get; set; } = true;

    [JsonPropertyName("blockDangerousShellCommandsByDefault")]
    public bool BlockDangerousShellCommandsByDefault { get; set; } = true;

    [JsonPropertyName("invalidateCacheOnPathChange")]
    public bool InvalidateCacheOnPathChange { get; set; } = true;

    [JsonPropertyName("cacheMustMatchActiveProfile")]
    public bool CacheMustMatchActiveProfile { get; set; } = true;
}
