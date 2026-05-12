using System.Text.Json.Serialization;
using RagnaForge.Api;

var builder = WebApplication.CreateBuilder(args);
var apiOptions = RagnaForgeApiOptions.FromConfiguration(builder.Configuration, builder.Environment);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = apiOptions.MaxRequestBodyBytes;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("RagnaForgeLocalCors", policy =>
    {
        policy
            .WithOrigins(apiOptions.AllowedOrigins.ToArray())
            .WithHeaders("Content-Type", apiOptions.ApiKeyHeaderName, ApiCorrelation.HeaderName)
            .WithMethods("GET", "POST", "OPTIONS")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

var workspaceRoot = ResolveWorkspaceRoot(builder.Configuration);

builder.Services.AddSingleton(apiOptions);
builder.Services.AddSingleton<ApiOperationGuard>();
builder.Services.AddSingleton<ApiKeyValidator>();
builder.Services.AddSingleton<ApiInMemoryRateLimiter>();
builder.Services.AddSingleton<ApiEndpointExecutor>();
builder.Services.AddSingleton(new RagnaForgeApiService(workspaceRoot, apiOptions));

var app = builder.Build();

app.UseMiddleware<ApiCorrelationMiddleware>();
app.UseMiddleware<ApiProblemHandlingMiddleware>();
app.UseStatusCodePages(async context =>
{
    var httpContext = context.HttpContext;
    if (httpContext.Response.HasStarted)
    {
        return;
    }

    var statusCode = httpContext.Response.StatusCode;
    var title = statusCode switch
    {
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status413PayloadTooLarge => "Payload too large",
        StatusCodes.Status429TooManyRequests => "Too many requests",
        _ => "Request failed"
    };
    var errorCode = statusCode switch
    {
        StatusCodes.Status404NotFound => "endpoint.not_found",
        StatusCodes.Status413PayloadTooLarge => "request.payload_too_large",
        StatusCodes.Status429TooManyRequests => "rate_limit.exceeded",
        _ => "request.failed"
    };

    await ApiProblemFactory.Result(
            httpContext,
            statusCode,
            title,
            title,
            errorCode)
        .ExecuteAsync(httpContext);
});
app.UseCors("RagnaForgeLocalCors");
app.UseMiddleware<ApiRequestSizeMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<ApiRateLimitingMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "ok",
    Mode = "read-only-dry-run-diff-preview",
    GeneratedAtUtc = DateTimeOffset.UtcNow
}))
.WithTags("System");

app.MapGet("/openapi/v1.json", (RagnaForgeApiOptions options) => Results.Ok(ApiOpenApiDocument.Create(options)))
    .WithTags("System");

var api = app.MapGroup("/api");

api.MapGet("/status", (HttpContext context, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.ReadOnly, service.GetStatus))
    .WithTags("System")
    .WithMetadata(new ApiOperationMetadata(OperationKind.ReadOnly, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

api.MapGet("/safety/capabilities", (HttpContext context, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.ReadOnly, () => ApiSafetyPolicy.Capabilities))
    .WithTags("System")
    .WithMetadata(new ApiOperationMetadata(OperationKind.ReadOnly, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

api.MapPost("/config/validate", (HttpContext context, ConfigRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.ReadOnly, () => service.ValidateConfig(request)))
    .WithTags("Config")
    .WithMetadata(new ApiOperationMetadata(OperationKind.ReadOnly, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

api.MapPost("/discover", (HttpContext context, DiscoveryRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.ReadOnly, () => service.Discover(request)))
    .WithTags("Discovery")
    .WithMetadata(new ApiOperationMetadata(OperationKind.ReadOnly, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

api.MapPost("/grf/index", (HttpContext context, GrfIndexRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.CacheWrite, () => service.IndexGrfs(request)))
    .WithTags("GRF")
    .WithMetadata(new ApiOperationMetadata(OperationKind.CacheWrite, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

api.MapPost("/grf/inspect", (HttpContext context, GrfInspectRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.CacheWrite, () => service.InspectGrf(request), () => Require(request.Container, nameof(request.Container))))
    .WithTags("GRF")
    .WithMetadata(new ApiOperationMetadata(OperationKind.CacheWrite, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

api.MapPost("/items/dry-run", (HttpContext context, ItemDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DryRun, () => service.CreateItemDryRun(request), () => Require(request.AegisName, nameof(request.AegisName), request.DisplayName, nameof(request.DisplayName))))
    .WithTags("Items")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DryRun, RagnaForgeApiPolicyNames.DryRunPolicy));

api.MapPost("/items/diff-preview", (HttpContext context, ItemDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DiffPreview, () => service.CreateItemDiffPreview(request), () => Require(request.AegisName, nameof(request.AegisName), request.DisplayName, nameof(request.DisplayName))))
    .WithTags("Items")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DiffPreview, RagnaForgeApiPolicyNames.DiffPreviewPolicy));

api.MapPost("/equipment/dry-run", (HttpContext context, EquipmentDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DryRun, () => service.CreateEquipmentDryRun(request), () => Require(request.AegisName, nameof(request.AegisName), request.DisplayName, nameof(request.DisplayName)).Concat(RequireAny(request.EquipLocations, nameof(request.EquipLocations))).ToArray()))
    .WithTags("Equipment")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DryRun, RagnaForgeApiPolicyNames.DryRunPolicy));

api.MapPost("/equipment/diff-preview", (HttpContext context, EquipmentDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DiffPreview, () => service.CreateEquipmentDiffPreview(request), () => Require(request.AegisName, nameof(request.AegisName), request.DisplayName, nameof(request.DisplayName)).Concat(RequireAny(request.EquipLocations, nameof(request.EquipLocations))).ToArray()))
    .WithTags("Equipment")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DiffPreview, RagnaForgeApiPolicyNames.DiffPreviewPolicy));

api.MapPost("/npcs/dry-run", (HttpContext context, NpcDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DryRun, () => service.CreateNpcDryRun(request), () => Require(request.Name, nameof(request.Name), request.MapName, nameof(request.MapName))))
    .WithTags("NPCs")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DryRun, RagnaForgeApiPolicyNames.DryRunPolicy));

api.MapPost("/npcs/diff-preview", (HttpContext context, NpcDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DiffPreview, () => service.CreateNpcDiffPreview(request), () => Require(request.Name, nameof(request.Name), request.MapName, nameof(request.MapName))))
    .WithTags("NPCs")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DiffPreview, RagnaForgeApiPolicyNames.DiffPreviewPolicy));

api.MapPost("/monsters/dry-run", (HttpContext context, MonsterDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DryRun, () => service.CreateMonsterDryRun(request), () => Require(request.AegisName, nameof(request.AegisName), request.DisplayName, nameof(request.DisplayName), request.MapName, nameof(request.MapName))))
    .WithTags("Monsters")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DryRun, RagnaForgeApiPolicyNames.DryRunPolicy));

api.MapPost("/monsters/diff-preview", (HttpContext context, MonsterDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DiffPreview, () => service.CreateMonsterDiffPreview(request), () => Require(request.AegisName, nameof(request.AegisName), request.DisplayName, nameof(request.DisplayName), request.MapName, nameof(request.MapName))))
    .WithTags("Monsters")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DiffPreview, RagnaForgeApiPolicyNames.DiffPreviewPolicy));

api.MapPost("/maps/dry-run", (HttpContext context, MapDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DryRun, () => service.CreateMapDryRun(request), () => Require(request.MapName, nameof(request.MapName))))
    .WithTags("Maps")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DryRun, RagnaForgeApiPolicyNames.DryRunPolicy));

api.MapPost("/maps/diff-preview", (HttpContext context, MapDryRunRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.DiffPreview, () => service.CreateMapDiffPreview(request), () => Require(request.MapName, nameof(request.MapName))))
    .WithTags("Maps")
    .WithMetadata(new ApiOperationMetadata(OperationKind.DiffPreview, RagnaForgeApiPolicyNames.DiffPreviewPolicy));

api.MapPost("/assets/preview", (HttpContext context, RagnaForge.Application.Assets.AssetPreviewRequest request, RagnaForgeApiService service, ApiEndpointExecutor executor) =>
    executor.Execute(context, OperationKind.ReadOnly, () => service.CreateAssetPreview(request, Guid.NewGuid().ToString("N")), () => Require(request.Source, nameof(request.Source), request.EntryPath, nameof(request.EntryPath), request.ExpectedExtension, nameof(request.ExpectedExtension))))
    .WithTags("Assets")
    .WithMetadata(new ApiOperationMetadata(OperationKind.ReadOnly, RagnaForgeApiPolicyNames.ReadOnlyPolicy));

app.Run();

static string ResolveWorkspaceRoot(IConfiguration configuration)
{
    var configured = configuration["RagnaForge:WorkspaceRoot"];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return Path.GetFullPath(configured);
    }

    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "RagnaForge.slnx"))
            || File.Exists(Path.Combine(current.FullName, "data", "manifests", "repositories.local.json")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static IReadOnlyList<string> Require(params string?[] valuesAndNames)
{
    var errors = new List<string>();
    for (var i = 0; i < valuesAndNames.Length; i += 2)
    {
        var value = valuesAndNames[i];
        var name = i + 1 < valuesAndNames.Length ? valuesAndNames[i + 1] : "field";
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
        }
    }

    return errors;
}

static IReadOnlyList<string> RequireAny<T>(IReadOnlyCollection<T>? values, string name)
{
    if (values is null || values.Count == 0)
    {
        return [$"{name} must contain at least one value."];
    }

    return [];
}
