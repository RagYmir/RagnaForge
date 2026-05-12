using System.Security.Cryptography;
using System.Text;

namespace RagnaForge.Api;

public sealed record ApiKeyValidationResult(bool Succeeded, string ErrorCode, string Detail)
{
    public static ApiKeyValidationResult Success { get; } = new(true, string.Empty, string.Empty);
}

public sealed class ApiKeyValidator(RagnaForgeApiOptions options, IHostEnvironment environment)
{
    public bool IsAuthenticationRequired =>
        options.RequireApiKey
        && !(environment.IsDevelopment() && options.AllowDevelopmentWithoutApiKey && string.IsNullOrWhiteSpace(options.ApiKey));

    public ApiKeyValidationResult Validate(IHeaderDictionary headers)
    {
        if (!IsAuthenticationRequired)
        {
            return ApiKeyValidationResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new ApiKeyValidationResult(false, "auth.api_key_not_configured", "API key authentication is required but no API key is configured.");
        }

        if (!headers.TryGetValue(options.ApiKeyHeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            return new ApiKeyValidationResult(false, "auth.api_key_missing", $"Missing required header '{options.ApiKeyHeaderName}'.");
        }

        var provided = values.FirstOrDefault()!;
        return ConstantTimeEquals(provided, options.ApiKey)
            ? ApiKeyValidationResult.Success
            : new ApiKeyValidationResult(false, "auth.api_key_invalid", "Invalid API key.");
    }

    private static bool ConstantTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}

public sealed class ApiKeyAuthenticationMiddleware(
    RequestDelegate next,
    ApiKeyValidator validator,
    ILogger<ApiKeyAuthenticationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPublicPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var validation = validator.Validate(context.Request.Headers);
        if (!validation.Succeeded)
        {
            logger.LogWarning(
                "API authentication failed. correlationId={CorrelationId} errorCode={ErrorCode} path={Path}",
                ApiCorrelation.Get(context),
                validation.ErrorCode,
                context.Request.Path.Value);

            var status = validation.ErrorCode == "auth.api_key_not_configured"
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status401Unauthorized;

            await ApiProblemFactory.Result(
                context,
                status,
                status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Authentication unavailable",
                validation.Detail,
                validation.ErrorCode)
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static bool IsPublicPath(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase);
}
