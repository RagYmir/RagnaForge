using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class KnowledgeSourcesTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_knowledge_sources";
    public string Description => "List all registered RagnaKnowledge sources. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new KnowledgeCommand(context.ConfigDir, context.AgentRoot, "sources", []).Execute());
}

public sealed class KnowledgeSearchTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_knowledge_search";
    public string Description => "Search for RagnaKnowledge entries based on query terms. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "string", maxLength = 512, description = "Query search terms (e.g. 'item_db', 'map dependencies')" }
        },
        required = new[] { "query" },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var dict = new Dictionary<string, string>();
        if (KnowledgeToolArguments.TryGetString(arguments, "query", out var query))
            dict["query"] = query;

        return McpResponseLimiter.Limit(new KnowledgeCommand(context.ConfigDir, context.AgentRoot, "search", dict).Execute());
    }
}

public sealed class KnowledgeExplainTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_knowledge_explain";
    public string Description => "Get a detailed explanation for a given topic or entity. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["topic"] = new { type = "string", maxLength = 512, description = "Topic to explain (e.g. 'rsw gnd gat map files', 'duplicate NPC names')" }
        },
        required = new[] { "topic" },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var dict = new Dictionary<string, string>();
        if (KnowledgeToolArguments.TryGetString(arguments, "topic", out var topic))
            dict["topic"] = topic;

        return McpResponseLimiter.Limit(new KnowledgeCommand(context.ConfigDir, context.AgentRoot, "explain", dict).Execute());
    }
}

public sealed class KnowledgeEntryTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_knowledge_entry";
    public string Description => "Retrieve a specific RagnaKnowledge entry details by ID. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["id"] = new { type = "string", maxLength = 128, description = "Specific entry ID (e.g. 'rathena.item.db_yaml')" }
        },
        required = new[] { "id" },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var dict = new Dictionary<string, string>();
        if (KnowledgeToolArguments.TryGetString(arguments, "id", out var id))
            dict["id"] = id;

        return McpResponseLimiter.Limit(new KnowledgeCommand(context.ConfigDir, context.AgentRoot, "entry", dict).Execute());
    }
}

public sealed class KnowledgeSchemaTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_knowledge_schema";
    public string Description => "Get property and validation constraints schema for a given entity type. Read-only.";
    public object InputSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["entity"] = new { type = "string", maxLength = 32, @enum = new[] { "item", "equipment", "mob", "npc", "map", "asset" }, description = "Entity type to show schema reference for." }
        },
        required = new[] { "entity" },
        additionalProperties = false
    };

    public JsonOutput Execute(JsonElement arguments)
    {
        var dict = new Dictionary<string, string>();
        if (KnowledgeToolArguments.TryGetString(arguments, "entity", out var entity))
            dict["entity"] = entity;

        return McpResponseLimiter.Limit(new KnowledgeCommand(context.ConfigDir, context.AgentRoot, "schema", dict).Execute());
    }
}

public sealed class KnowledgeValidateTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_knowledge_validate";
    public string Description => "Validate RagnaKnowledge packs integrity. Read-only.";
    public object InputSchema => SchemaFactory.Empty();
    public JsonOutput Execute(JsonElement arguments) =>
        McpResponseLimiter.Limit(new KnowledgeCommand(context.ConfigDir, context.AgentRoot, "validate", []).Execute());
}

internal static class KnowledgeToolArguments
{
    public static bool TryGetString(JsonElement arguments, string propertyName, out string value)
    {
        value = string.Empty;
        if (!arguments.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
            return false;

        value = element.GetString() ?? string.Empty;
        return true;
    }
}
