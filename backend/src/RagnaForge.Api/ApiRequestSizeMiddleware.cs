namespace RagnaForge.Api;

public sealed class ApiRequestSizeMiddleware(RequestDelegate next, RagnaForgeApiOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength is > 0
            && context.Request.ContentLength > options.MaxRequestBodyBytes)
        {
            await ApiProblemFactory.Result(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "Payload too large",
                $"Request body exceeds configured maximum of {options.MaxRequestBodyBytes} bytes.",
                "request.payload_too_large")
                .ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
