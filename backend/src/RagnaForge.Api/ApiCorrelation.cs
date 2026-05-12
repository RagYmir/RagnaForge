namespace RagnaForge.Api;

public static class ApiCorrelation
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "RagnaForge.CorrelationId";

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string correlationId
            ? correlationId
            : string.Empty;

    public static string Normalize(string? candidate) =>
        string.IsNullOrWhiteSpace(candidate) || candidate.Length > 128
            ? Guid.NewGuid().ToString("N")
            : candidate.Trim();
}

public sealed class ApiCorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ApiCorrelation.Normalize(context.Request.Headers[ApiCorrelation.HeaderName].FirstOrDefault());
        context.Items[ApiCorrelation.ItemKey] = correlationId;
        context.Response.Headers[ApiCorrelation.HeaderName] = correlationId;

        await next(context);
    }
}
