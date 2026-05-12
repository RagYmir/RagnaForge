namespace RagnaForge.Api;

public sealed class RagnaForgeApiOptions
{
    public bool ReadOnlyMode { get; init; } = true;
    public bool EnableApplyEndpoints { get; init; }
    public bool EnableRollbackEndpoints { get; init; }
    public bool RequireApiKey { get; init; } = true;
    public bool AllowDevelopmentWithoutApiKey { get; init; }
    public string ApiKeyHeaderName { get; init; } = "X-RagnaForge-Api-Key";
    public string? ApiKey { get; init; }
    public IReadOnlyList<string> AllowedOrigins { get; init; } =
    [
        "http://127.0.0.1:5173",
        "http://localhost:5173"
    ];
    public long MaxRequestBodyBytes { get; init; } = 1_048_576;
    public int MaxGrfContainersPerRequest { get; init; } = 50;
    public int MaxDiffHunksPerResponse { get; init; } = 500;
    public int MaxScanSeconds { get; init; } = 60;
    public int GlobalRequestsPerMinute { get; init; } = 60;
    public int GrfRequestsPerMinute { get; init; } = 5;
    public int MapRequestsPerMinute { get; init; } = 10;
    public int HeavyOperationConcurrency { get; init; } = 2;
    public bool IncludeExceptionDetails { get; init; }

    public static RagnaForgeApiOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection("RagnaForge:Api");
        var options = new RagnaForgeApiOptions
        {
            ReadOnlyMode = section.GetValue("ReadOnlyMode", true),
            EnableApplyEndpoints = section.GetValue("EnableApplyEndpoints", false),
            EnableRollbackEndpoints = section.GetValue("EnableRollbackEndpoints", false),
            RequireApiKey = section.GetValue("RequireApiKey", true),
            AllowDevelopmentWithoutApiKey = section.GetValue("AllowDevelopmentWithoutApiKey", false),
            ApiKeyHeaderName = section.GetValue("ApiKeyHeaderName", "X-RagnaForge-Api-Key") ?? "X-RagnaForge-Api-Key",
            ApiKey = section.GetValue<string?>("ApiKey") ?? Environment.GetEnvironmentVariable("RAGNAFORGE_API_KEY"),
            AllowedOrigins = section.GetSection("AllowedOrigins").Get<string[]?>() ??
            [
                "http://127.0.0.1:5173",
                "http://localhost:5173"
            ],
            MaxRequestBodyBytes = section.GetValue("MaxRequestBodyBytes", 1_048_576L),
            MaxGrfContainersPerRequest = section.GetValue("MaxGrfContainersPerRequest", 50),
            MaxDiffHunksPerResponse = section.GetValue("MaxDiffHunksPerResponse", 500),
            MaxScanSeconds = section.GetValue("MaxScanSeconds", 60),
            GlobalRequestsPerMinute = section.GetValue("GlobalRequestsPerMinute", 60),
            GrfRequestsPerMinute = section.GetValue("GrfRequestsPerMinute", 5),
            MapRequestsPerMinute = section.GetValue("MapRequestsPerMinute", 10),
            HeavyOperationConcurrency = section.GetValue("HeavyOperationConcurrency", 2),
            IncludeExceptionDetails = section.GetValue("IncludeExceptionDetails", false)
        };

        if (!environment.IsDevelopment() && options.AllowDevelopmentWithoutApiKey)
        {
            options = options.WithDevelopmentBypassDisabled();
        }

        return options;
    }

    private RagnaForgeApiOptions WithDevelopmentBypassDisabled() => new()
    {
        ReadOnlyMode = ReadOnlyMode,
        EnableApplyEndpoints = EnableApplyEndpoints,
        EnableRollbackEndpoints = EnableRollbackEndpoints,
        RequireApiKey = RequireApiKey,
        AllowDevelopmentWithoutApiKey = false,
        ApiKeyHeaderName = ApiKeyHeaderName,
        ApiKey = ApiKey,
        AllowedOrigins = AllowedOrigins,
        MaxRequestBodyBytes = MaxRequestBodyBytes,
        MaxGrfContainersPerRequest = MaxGrfContainersPerRequest,
        MaxDiffHunksPerResponse = MaxDiffHunksPerResponse,
        MaxScanSeconds = MaxScanSeconds,
        GlobalRequestsPerMinute = GlobalRequestsPerMinute,
        GrfRequestsPerMinute = GrfRequestsPerMinute,
        MapRequestsPerMinute = MapRequestsPerMinute,
        HeavyOperationConcurrency = HeavyOperationConcurrency,
        IncludeExceptionDetails = IncludeExceptionDetails
    };
}

public static class RagnaForgeApiPolicyNames
{
    public const string ReadOnlyPolicy = "ReadOnlyPolicy";
    public const string DryRunPolicy = "DryRunPolicy";
    public const string DiffPreviewPolicy = "DiffPreviewPolicy";
    public const string AdminPolicy = "AdminPolicy";
    public const string DangerousOperationPolicy = "DangerousOperationPolicy";
}
