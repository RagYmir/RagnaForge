using System;
using System.Collections.Generic;

namespace RagnaForge.Api;

public sealed record PipelineStatusResponse(
    bool ApiReadOnly,
    bool DryRunAvailable,
    bool DiffPreviewAvailable,
    bool ApplyAvailable,
    bool RollbackRealAvailable,
    AgentHealthSummary? AgentHealthSummary,
    bool SafeForReadOnlyWork,
    bool SafeForDryRun,
    bool SafeForApply,
    int ExternalDataIssueCount,
    IReadOnlyList<string> CurrentKnownLimitations);

public sealed record PipelineIssuesResponse(
    bool ReadOnly,
    bool SafeForReadOnlyWork,
    bool SafeForDryRun,
    bool SafeForApply,
    PipelineIssueSummary Summary,
    IReadOnlyList<PipelineIssueReference> Issues,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public sealed record PipelinePlanRequest(
    string EntityType,
    string Mode,
    System.Text.Json.JsonElement Payload,
    string? SourceHints,
    bool IncludeAssets,
    bool IncludeClientSide,
    bool IncludeServerSide);

public sealed record PipelinePlanResponse(
    string OperationId,
    bool ReadOnly,
    string EntityType,
    PipelineDependencySummary DependencySummary,
    PipelineIssueSummary ValidationSummary,
    IReadOnlyList<PipelineStep> PlannedSteps,
    IReadOnlyList<PipelineStep> BlockedSteps,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    PipelineReadiness Readiness,
    PipelineLinks Links);

public sealed record PipelineLinks(
    string DryRun,
    string DiffPreview,
    string Report);

public sealed record PipelineDryRunRequest(
    string OperationId,
    string EntityType,
    System.Text.Json.JsonElement Payload);

public sealed record PipelineDryRunResponse(
    string OperationId,
    bool NoPersistentWrites,
    object DryRunReport,
    IReadOnlyList<string> GeneratedFilesPreview,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool SafeForApply);

public sealed record PipelineDiffPreviewResponse(
    string OperationId,
    bool NoPersistentWrites,
    IReadOnlyList<PipelineDiffEntry> DiffByFile,
    int Additions,
    int Modifications,
    int Deletions,
    string RiskLevel);

public sealed record PipelineDiffEntry(
    string TargetPath,
    string ChangeKind,
    bool Exists,
    string? UnifiedDiff,
    string? Preview);

public sealed record PipelineDependencySummary(
    IReadOnlyList<PipelineDependencyItem> ServerDb,
    IReadOnlyList<PipelineDependencyItem> ClientDb,
    IReadOnlyList<PipelineDependencyItem> Scripts,
    IReadOnlyList<PipelineDependencyItem> Assets);

public sealed record PipelineDependencyItem(
    string Name,
    string Type,
    string Status, // Present | Missing | Ambiguous | ExternalDataWarning | Blocked | Unsupported | Placeholder | NotChecked
    string ExpectedPath,
    string Source, // e.g. "rAthena", "Patch", "GRF", "Agent Index"
    string? Notes);

public sealed record PipelineReadiness(
    bool CanInspect,
    bool CanDryRun,
    bool CanDiffPreview,
    bool CanApply);

public sealed record PipelineStep(
    string Name,
    string Action,
    string Target,
    string Status, // Pending | Blocked | Completed
    string? Reason);

public sealed record PipelineIssueSummary(
    int Total,
    int Errors,
    int Warnings,
    IReadOnlyList<PipelineIssueReference> Issues,
    int ExternalDataCount,
    int ApplyBlockersCount,
    int DryRunBlockersCount);

public sealed record PipelineIssueReference(
    string Code,
    string Severity,
    string Message,
    string Scope,
    string EntityName,
    string? SourceFile);

public sealed record PipelineReportSummary(
    string Id,
    string Title,
    string EntityType,
    DateTimeOffset GeneratedAtUtc,
    long SizeBytes);
