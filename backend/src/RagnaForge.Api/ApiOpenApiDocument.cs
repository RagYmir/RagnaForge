namespace RagnaForge.Api;

public static class ApiOpenApiDocument
{
    public static object Create(RagnaForgeApiOptions options) => new
    {
        openapi = "3.1.0",
        info = new
        {
            title = "RagnaForge API",
            version = "1.0.0-readonly",
            description = "Local read-only/dry-run/diff-preview API. Apply and rollback endpoints are intentionally absent."
        },
        servers = new[]
        {
            new { url = "http://127.0.0.1:5099", description = "Local development" }
        },
        security = new[]
        {
            new Dictionary<string, string[]> { ["RagnaForgeApiKey"] = [] }
        },
        components = new
        {
            securitySchemes = new Dictionary<string, object>
            {
                ["RagnaForgeApiKey"] = new
                {
                    type = "apiKey",
                    name = options.ApiKeyHeaderName,
                    @in = "header",
                    description = "Local API key. Do not expose beyond trusted local clients."
                }
            }
        },
        tags = new[]
        {
            new { name = "System" },
            new { name = "Config" },
            new { name = "Discovery" },
            new { name = "GRF" },
            new { name = "Items" },
            new { name = "Equipment" },
            new { name = "NPCs" },
            new { name = "Monsters" },
            new { name = "Maps" },
            new { name = "Assets" },
            new { name = "Agent" },
            new { name = "Pipeline" }
        },
        x_ragnaforge = new
        {
            options.ReadOnlyMode,
            options.EnableApplyEndpoints,
            options.EnableRollbackEndpoints,
            dangerousEndpoints = "Apply and rollback are not mapped in this API version.",
            problemDetails = "Errors include errorCode, correlationId, path and timestamp.",
            correlationHeader = ApiCorrelation.HeaderName
        },
        paths = new Dictionary<string, object>
        {
            ["/health"] = Path("System", false),
            ["/api/status"] = Path("System", true),
            ["/api/config/validate"] = Path("Config", true),
            ["/api/discover"] = Path("Discovery", true),
            ["/api/grf/index"] = Path("GRF", true),
            ["/api/grf/inspect"] = Path("GRF", true),
            ["/api/items/dry-run"] = Path("Items", true),
            ["/api/items/diff-preview"] = Path("Items", true),
            ["/api/equipment/dry-run"] = Path("Equipment", true),
            ["/api/equipment/diff-preview"] = Path("Equipment", true),
            ["/api/npcs/dry-run"] = Path("NPCs", true),
            ["/api/npcs/diff-preview"] = Path("NPCs", true),
            ["/api/monsters/dry-run"] = Path("Monsters", true),
            ["/api/monsters/diff-preview"] = Path("Monsters", true),
            ["/api/maps/dry-run"] = Path("Maps", true),
            ["/api/maps/diff-preview"] = Path("Maps", true),
            ["/api/assets/preview"] = PostPath("Assets", true),
            ["/api/agent/health"] = GetPath("Agent", true),
            ["/api/pipeline/status"] = GetPath("Pipeline", true),
            ["/api/pipeline/plan"] = PostPath("Pipeline", true),
            ["/api/pipeline/dry-run"] = PostPath("Pipeline", true),
            ["/api/pipeline/diff-preview"] = PostPath("Pipeline", true),
            ["/api/pipeline/issues"] = GetPath("Pipeline", true),
            ["/api/pipeline/reports"] = GetPath("Pipeline", true),
            ["/api/pipeline/reports/{id}"] = GetPath("Pipeline", true)
        }
    };

    private static object Path(string tag, bool secured) => new
    {
        post = tag == "System" ? null : Operation(tag, secured),
        get = tag == "System" ? Operation(tag, secured) : null
    };

    private static object GetPath(string tag, bool secured) => new
    {
        get = Operation(tag, secured)
    };

    private static object PostPath(string tag, bool secured) => new
    {
        post = Operation(tag, secured)
    };

    private static object Operation(string tag, bool secured) => new
    {
        tags = new[] { tag },
        security = secured ? new[] { new Dictionary<string, string[]> { ["RagnaForgeApiKey"] = [] } } : [],
        responses = new Dictionary<string, object>
        {
            ["200"] = new { description = "Success ApiResponse" },
            ["400"] = new { description = "Bad request ProblemDetails" },
            ["401"] = new { description = "Unauthorized ProblemDetails" },
            ["403"] = new { description = "Forbidden ProblemDetails" },
            ["413"] = new { description = "Payload too large ProblemDetails" },
            ["422"] = new { description = "Domain validation ProblemDetails" },
            ["429"] = new { description = "Rate limit ProblemDetails" },
            ["500"] = new { description = "Unexpected error ProblemDetails" }
        }
    };
}
