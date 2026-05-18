using System.Diagnostics;

namespace RagnaForge.Api;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    DateTimeOffset GeneratedAt,
    string CorrelationId,
    OperationKind OperationKind,
    bool ReadOnlyMode,
    long DurationMs);

public sealed class ApiEndpointExecutor(
    ApiOperationGuard operationGuard,
    RagnaForgeApiOptions options,
    ILogger<ApiEndpointExecutor> logger)
{
    public IResult Execute<T>(
        HttpContext context,
        OperationKind operationKind,
        Func<T> action,
        Func<IReadOnlyList<string>>? validate = null)
    {
        var stopwatch = Stopwatch.StartNew();
        operationGuard.EnsureAllowed(operationKind);

        var validationErrors = validate?.Invoke() ?? [];
        if (validationErrors.Count > 0)
        {
            throw ApiException.Unprocessable(
                "payload.validation_failed",
                "Request payload failed validation.",
                new Dictionary<string, string[]> { ["request"] = validationErrors.ToArray() });
        }

        var data = action();
        if (operationKind == OperationKind.DiffPreview && data is not null)
        {
            operationGuard.EnsureDiffLimit(data);
        }

        stopwatch.Stop();
        var response = new ApiResponse<T>(
            true,
            data,
            ExtractWarnings(data),
            [],
            DateTimeOffset.UtcNow,
            ApiCorrelation.Get(context),
            operationKind,
            options.ReadOnlyMode,
            stopwatch.ElapsedMilliseconds);

        logger.LogInformation(
            "API request completed. correlationId={CorrelationId} operationKind={OperationKind} path={Path} durationMs={DurationMs} warnings={WarningsCount}",
            response.CorrelationId,
            operationKind,
            context.Request.Path.Value,
            response.DurationMs,
            response.Warnings.Count);

        return Results.Ok(response);
    }

    public async Task<IResult> ExecuteAsync<T>(
        HttpContext context,
        OperationKind operationKind,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<IReadOnlyList<string>>? validate = null)
    {
        var stopwatch = Stopwatch.StartNew();
        operationGuard.EnsureAllowed(operationKind);

        var validationErrors = validate?.Invoke() ?? [];
        if (validationErrors.Count > 0)
        {
            throw ApiException.Unprocessable(
                "payload.validation_failed",
                "Request payload failed validation.",
                new Dictionary<string, string[]> { ["request"] = validationErrors.ToArray() });
        }

        var data = await action(cancellationToken);
        if (operationKind == OperationKind.DiffPreview && data is not null)
        {
            operationGuard.EnsureDiffLimit(data);
        }

        stopwatch.Stop();
        var response = new ApiResponse<T>(
            true,
            data,
            ExtractWarnings(data),
            [],
            DateTimeOffset.UtcNow,
            ApiCorrelation.Get(context),
            operationKind,
            options.ReadOnlyMode,
            stopwatch.ElapsedMilliseconds);

        logger.LogInformation(
            "API request completed. correlationId={CorrelationId} operationKind={OperationKind} path={Path} durationMs={DurationMs} warnings={WarningsCount}",
            response.CorrelationId,
            operationKind,
            context.Request.Path.Value,
            response.DurationMs,
            response.Warnings.Count);

        return Results.Ok(response);
    }

    private static IReadOnlyList<string> ExtractWarnings<T>(T data)
    {
        if (data is null)
        {
            return [];
        }

        var property = data.GetType().GetProperty("ValidationWarnings")
            ?? data.GetType().GetProperty("Warnings");

        return property?.GetValue(data) switch
        {
            IReadOnlyList<string> warnings => warnings,
            IEnumerable<string> warnings => warnings.ToArray(),
            _ => []
        };
    }
}
