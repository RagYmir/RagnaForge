namespace RagnaForge.Domain.Configuration;

public sealed record ManifestValidationIssue(
    ManifestValidationSeverity Severity,
    string Code,
    string Message);

public enum ManifestValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}
