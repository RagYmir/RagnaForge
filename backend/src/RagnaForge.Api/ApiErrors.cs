using Microsoft.AspNetCore.Mvc;

namespace RagnaForge.Api;

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }
    public string Title { get; }
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    public ApiException(
        int statusCode,
        string errorCode,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(detail)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Title = title;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }

    public static ApiException BadRequest(string errorCode, string detail) =>
        new(StatusCodes.Status400BadRequest, errorCode, "Bad request", detail);

    public static ApiException Unauthorized(string errorCode, string detail) =>
        new(StatusCodes.Status401Unauthorized, errorCode, "Unauthorized", detail);

    public static ApiException Forbidden(string errorCode, string detail) =>
        new(StatusCodes.Status403Forbidden, errorCode, "Forbidden", detail);

    public static ApiException Conflict(string errorCode, string detail) =>
        new(StatusCodes.Status409Conflict, errorCode, "Conflict", detail);

    public static ApiException PayloadTooLarge(string errorCode, string detail) =>
        new(StatusCodes.Status413PayloadTooLarge, errorCode, "Payload too large", detail);

    public static ApiException Unprocessable(string errorCode, string detail, IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(StatusCodes.Status422UnprocessableEntity, errorCode, "Validation failed", detail, validationErrors);

    public static ApiException TooManyRequests(string errorCode, string detail) =>
        new(StatusCodes.Status429TooManyRequests, errorCode, "Too many requests", detail);
}

public static class ApiProblemFactory
{
    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["correlationId"] = ApiCorrelation.Get(context);
        problem.Extensions["path"] = context.Request.Path.ToString();
        problem.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        if (validationErrors is { Count: > 0 })
        {
            problem.Extensions["validationErrors"] = validationErrors;
        }

        return problem;
    }

    public static IResult Result(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        var problem = Create(context, statusCode, title, detail, errorCode, validationErrors);
        return Results.Json(problem, statusCode: statusCode, contentType: "application/problem+json");
    }
}

public sealed class ApiProblemHandlingMiddleware(
    RequestDelegate next,
    ILogger<ApiProblemHandlingMiddleware> logger,
    RagnaForgeApiOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(
                "API request blocked. correlationId={CorrelationId} errorCode={ErrorCode} statusCode={StatusCode} path={Path}",
                ApiCorrelation.Get(context),
                ex.ErrorCode,
                ex.StatusCode,
                context.Request.Path.Value);

            await WriteProblemAsync(context, ex.StatusCode, ex.Title, ex.Message, ex.ErrorCode, ex.ValidationErrors);
        }
        catch (BadHttpRequestException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad request", ex.Message, "request.invalid");
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity, "Validation failed", ex.Message, "domain.validation_failed");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected API error. correlationId={CorrelationId} path={Path}",
                ApiCorrelation.Get(context),
                context.Request.Path.Value);

            var detail = options.IncludeExceptionDetails
                ? ex.ToString()
                : "Unexpected API error. See logs using the correlationId.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Unexpected error", detail, "server.unexpected");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = ApiProblemFactory.Create(context, statusCode, title, detail, errorCode, validationErrors);
        await context.Response.WriteAsJsonAsync(problem);
    }
}
