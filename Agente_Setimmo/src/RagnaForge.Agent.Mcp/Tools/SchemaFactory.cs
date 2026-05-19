namespace RagnaForge.Agent.Mcp.Tools;

internal static class SchemaFactory
{
    public static object Empty() => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        additionalProperties = false
    };

    public static object Find(bool allowId) => new
    {
        type = "object",
        properties = allowId
            ? new Dictionary<string, object>
            {
                ["id"] = new { type = "integer" },
                ["name"] = new { type = "string" }
            }
            : new Dictionary<string, object>
            {
                ["name"] = new { type = "string" }
            },
        additionalProperties = false
    };

    public static object Operation() => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["operationId"] = new { type = "string" },
            ["last"] = new { type = "boolean" },
            ["format"] = new { type = "string", @enum = new[] { "json", "md" } }
        },
        additionalProperties = false
    };

    public static object DryRun() => new
    {
        type = "object",
        additionalProperties = true
    };
}
