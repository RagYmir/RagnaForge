namespace RagnaForge.Domain.Configuration;

public sealed record ManifestValidationResult(IReadOnlyList<ManifestValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != ManifestValidationSeverity.Error);

    public static ManifestValidationResult Valid { get; } = new([]);
}
