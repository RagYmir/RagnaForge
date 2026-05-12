using System.Collections.Concurrent;

namespace RagnaForge.Api;

public sealed record ApiRateLimitPolicy(string Name, int RequestsPerMinute, bool Heavy);

public sealed class ApiInMemoryRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _requests = new();

    public bool TryAcquire(string key, int limit, TimeSpan window, DateTimeOffset now, out TimeSpan retryAfter)
    {
        var queue = _requests.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= limit)
            {
                retryAfter = window - (now - queue.Peek());
                return false;
            }

            queue.Enqueue(now);
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }
}

public sealed class ApiRateLimitingMiddleware(
    RequestDelegate next,
    RagnaForgeApiOptions options,
    ApiInMemoryRateLimiter limiter,
    ILogger<ApiRateLimitingMiddleware> logger)
{
    private readonly SemaphoreSlim _heavyOperationSemaphore = new(Math.Max(1, options.HeavyOperationConcurrency));

    public async Task InvokeAsync(HttpContext context)
    {
        var policy = ResolvePolicy(context.Request.Path);
        var clientKey = $"{policy.Name}:{context.Connection.RemoteIpAddress ?? System.Net.IPAddress.Loopback}";

        if (!limiter.TryAcquire(clientKey, Math.Max(1, policy.RequestsPerMinute), TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            logger.LogWarning(
                "API rate limit rejected request. correlationId={CorrelationId} policy={Policy} path={Path}",
                ApiCorrelation.Get(context),
                policy.Name,
                context.Request.Path.Value);

            await ApiProblemFactory.Result(
                context,
                StatusCodes.Status429TooManyRequests,
                "Too many requests",
                $"Rate limit exceeded for policy '{policy.Name}'.",
                "rate_limit.exceeded")
                .ExecuteAsync(context);
            return;
        }

        if (!policy.Heavy)
        {
            await next(context);
            return;
        }

        if (!await _heavyOperationSemaphore.WaitAsync(0))
        {
            await ApiProblemFactory.Result(
                context,
                StatusCodes.Status429TooManyRequests,
                "Too many requests",
                "Concurrent heavy operation limit reached.",
                "concurrency.heavy_operation_limit")
                .ExecuteAsync(context);
            return;
        }

        try
        {
            await next(context);
        }
        finally
        {
            _heavyOperationSemaphore.Release();
        }
    }

    private ApiRateLimitPolicy ResolvePolicy(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.Contains("/grf/", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiRateLimitPolicy("grf", options.GrfRequestsPerMinute, true);
        }

        if (value.Contains("/maps/", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiRateLimitPolicy("map", options.MapRequestsPerMinute, true);
        }

        if (value.Contains("/discover", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/config/", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiRateLimitPolicy("config-discovery", Math.Max(1, options.GlobalRequestsPerMinute / 2), false);
        }

        return new ApiRateLimitPolicy("global", options.GlobalRequestsPerMinute, false);
    }
}
