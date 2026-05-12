namespace RagnaForge.Api;

public enum OperationKind
{
    ReadOnly,
    DryRun,
    DiffPreview,
    Apply,
    Rollback,
    FileWrite,
    CacheWrite,
    ExternalRepoWrite,
    GrfWrite
}

public sealed record ApiOperationMetadata(OperationKind OperationKind, string PolicyName);

public sealed class ApiOperationGuard(RagnaForgeApiOptions options)
{
    public void EnsureAllowed(OperationKind operationKind)
    {
        if (!IsAllowed(operationKind))
        {
            throw ApiException.Forbidden(
                "operation.blocked",
                $"Operation '{operationKind}' is blocked by the API safety policy.");
        }
    }

    public bool IsAllowed(OperationKind operationKind) =>
        operationKind switch
        {
            OperationKind.ReadOnly => true,
            OperationKind.DryRun => true,
            OperationKind.DiffPreview => true,
            OperationKind.CacheWrite => true,
            OperationKind.Apply => options.EnableApplyEndpoints && !options.ReadOnlyMode && ApiSafetyPolicy.IsWriteOperationEnabled("apply"),
            OperationKind.Rollback => options.EnableRollbackEndpoints && !options.ReadOnlyMode && ApiSafetyPolicy.IsWriteOperationEnabled("rollback"),
            OperationKind.FileWrite => false,
            OperationKind.ExternalRepoWrite => false,
            OperationKind.GrfWrite => false,
            _ => false
        };

    public void EnsureGrfContainerLimit(int requested)
    {
        if (requested > options.MaxGrfContainersPerRequest)
        {
            throw ApiException.Unprocessable(
                "limit.grf_containers",
                $"Requested GRF container limit '{requested}' exceeds configured maximum '{options.MaxGrfContainersPerRequest}'.");
        }
    }

    public void EnsureDiffLimit(object result)
    {
        var fileCountProperty = result.GetType().GetProperty("FileCount");
        if (fileCountProperty?.GetValue(result) is int fileCount
            && fileCount > options.MaxDiffHunksPerResponse)
        {
            throw ApiException.Unprocessable(
                "limit.diff_hunks",
                $"Diff response has '{fileCount}' hunks and exceeds configured maximum '{options.MaxDiffHunksPerResponse}'.");
        }
    }
}
