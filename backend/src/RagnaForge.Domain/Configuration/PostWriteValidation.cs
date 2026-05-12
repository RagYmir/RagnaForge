namespace RagnaForge.Domain.Configuration;

public enum PostWriteValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record PostWriteValidationPlanEntry(
    string TargetPath,
    string ValidatorName,
    string Purpose);

public sealed record PostWriteValidationIssue(
    string Code,
    string Message,
    PostWriteValidationSeverity Severity);

public sealed record PostWriteValidationFileResult(
    string TargetPath,
    string StagingPath,
    string ValidatorName,
    bool IsValid,
    IReadOnlyList<PostWriteValidationIssue> Issues);

public sealed record PostWriteValidationSummary(
    bool IsValid,
    IReadOnlyList<PostWriteValidationFileResult> Files);
