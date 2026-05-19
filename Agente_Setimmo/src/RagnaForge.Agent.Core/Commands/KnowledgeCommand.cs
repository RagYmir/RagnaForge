using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RagnaForge.Agent.Core.Knowledge;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Core.Commands;

public sealed class KnowledgeCommand
{
    private static readonly HashSet<string> AllowedSchemaEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        "item",
        "equipment",
        "mob",
        "npc",
        "map",
        "asset"
    };

    private readonly string _configDir;
    private readonly string _agentRoot;
    private readonly string _subCommand;
    private readonly Dictionary<string, string> _params;

    public KnowledgeCommand(string configDir, string agentRoot, string subCommand, Dictionary<string, string> parameters)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
        _subCommand = subCommand.ToLowerInvariant();
        _params = parameters;
    }

    public JsonOutput Execute()
    {
        try
        {
            var service = new KnowledgeService(_agentRoot);

            switch (_subCommand)
            {
                case "sources":
                    return ExecuteSources(service);

                case "build":
                    return ExecuteBuild(service);

                case "search":
                    return ExecuteSearch(service);

                case "explain":
                    return ExecuteExplain(service);

                case "entry":
                    return ExecuteEntry(service);

                case "validate":
                    return ExecuteValidate(service);

                case "schema":
                    return ExecuteSchema(service);

                default:
                    return JsonOutput.Error("knowledge", $"Unknown knowledge subcommand: '{_subCommand}'");
            }
        }
        catch (Exception)
        {
            return JsonOutput.Error("knowledge", "Knowledge command failed safely. Review local agent logs for technical details.");
        }
    }

    private JsonOutput ExecuteSources(KnowledgeService service)
    {
        var sources = service.LoadSources();
        var output = JsonOutput.Success("knowledge-sources", $"Loaded {sources.Count} knowledge sources successfully.");
        output.Data = new { sources };
        return output;
    }

    private JsonOutput ExecuteBuild(KnowledgeService service)
    {
        var index = service.BuildIndex();
        var output = JsonOutput.Success("knowledge-build", $"Built index successfully with {index.Entries.Count} entries.");
        output.Data = new { entriesCount = index.Entries.Count, index };
        return output;
    }

    private JsonOutput ExecuteSearch(KnowledgeService service)
    {
        _params.TryGetValue("query", out var queryStr);
        var validationError = ValidateInput(queryStr, "query", 512);
        if (validationError is not null)
        {
            return JsonOutput.Error("knowledge-search", validationError);
        }

        var queryValue = queryStr!;
        var query = new KnowledgeQuery
        {
            Query = queryValue,
            Limit = 10,
            IncludeDetails = true,
            IncludeSources = true
        };

        var results = service.Search(query);
        var output = JsonOutput.Success("knowledge-search", $"Found {results.Count} matching knowledge entries.");
        AddIndexWarning(service, output);
        output.Data = new { results };
        return output;
    }

    private JsonOutput ExecuteExplain(KnowledgeService service)
    {
        _params.TryGetValue("topic", out var topicStr);
        var validationError = ValidateInput(topicStr, "topic", 512);
        if (validationError is not null)
        {
            return JsonOutput.Error("knowledge-explain", validationError);
        }

        var topicValue = topicStr!;
        var results = service.Explain(topicValue);
        var output = JsonOutput.Success("knowledge-explain", $"Explanation details for topic: '{topicValue}'");
        AddIndexWarning(service, output);
        output.Data = new { topic = topicValue, results };
        return output;
    }

    private JsonOutput ExecuteEntry(KnowledgeService service)
    {
        _params.TryGetValue("id", out var idStr);
        var validationError = ValidateInput(idStr, "id", 128);
        if (validationError is not null)
        {
            return JsonOutput.Error("knowledge-entry", validationError);
        }

        var idValue = idStr!;
        var entry = service.GetEntry(idValue);
        if (entry == null)
        {
            return JsonOutput.Error("knowledge-entry", "Knowledge entry not found.");
        }

        var output = JsonOutput.Success("knowledge-entry", $"Loaded entry: '{entry.Title}'");
        AddIndexWarning(service, output);
        output.Data = new { entry };
        return output;
    }

    private JsonOutput ExecuteValidate(KnowledgeService service)
    {
        var issues = service.ValidatePacks();
        var ok = issues.Count == 0;

        var output = new JsonOutput
        {
            Ok = ok,
            Mode = "knowledge-validate",
            Summary = ok ? "Knowledge validation passed. All packs are clean." : $"Knowledge validation failed with {issues.Count} issue(s).",
            SafeForAutomation = ok,
            Errors = issues
        };

        return output;
    }

    private JsonOutput ExecuteSchema(KnowledgeService service)
    {
        _params.TryGetValue("entity", out var entityStr);
        var validationError = ValidateInput(entityStr, "entity", 32);
        if (validationError is not null)
        {
            return JsonOutput.Error("knowledge-schema", validationError);
        }

        var entityType = entityStr!.ToLowerInvariant();
        if (!AllowedSchemaEntities.Contains(entityType))
        {
            return JsonOutput.Error("knowledge-schema", "Unsupported entity type for schema. Supported: item, equipment, mob, npc, map, asset.");
        }

        object? schema = null;

        switch (entityType)
        {
            case "item":
                schema = new
                {
                    entityType = "item",
                    primaryFields = new[] { "Id", "AegisName" },
                    databaseFields = new[] { "Id (int)", "AegisName (string)", "Type (string)", "Weight (int)", "Buy (int)", "Sell (int)", "Slots (int)", "Script (string)" },
                    clientPairing = "System/itemInfo.lua (or .lub) mapped by Id. unidentifed/identified display and resource names.",
                    validationRules = new[] { "AegisName and Id must be globally unique.", "Duplicate Id within server is critical error, duplicates on client-side entries yield warnings." }
                };
                break;

            case "equipment":
                schema = new
                {
                    entityType = "equipment",
                    primaryFields = new[] { "Id", "AegisName", "View" },
                    databaseFields = new[] { "View (int)", "EquipLocations (string)", "JobRestrictions (string)", "WeaponType (string)" },
                    clientPairing = "Weapon/Headgear View maps directly to visual folder paths in data/sprite. Shields use hardcoded built-in View ranges.",
                    validationRules = new[] { "Shield views outside default built-in indices are blocked.", "Duplicate headgear or weapon View IDs are flagged." }
                };
                break;

            case "mob":
                schema = new
                {
                    entityType = "mob",
                    primaryFields = new[] { "Id", "AegisName" },
                    databaseFields = new[] { "Id (int)", "AegisName (string)", "Name (string)", "Hp (int)", "Drops (array)", "Skills (array)", "Spawns (array)" },
                    clientPairing = "Loads custom monster sprites based on AegisName from data/sprite/monster/AegisName.spr/.act.",
                    validationRules = new[] { "Duplicate Mob IDs on server are forbidden.", "Drops must reference existing server-side Item IDs." }
                };
                break;

            case "npc":
                schema = new
                {
                    entityType = "npc",
                    primaryFields = new[] { "DisplayName", "SpriteID" },
                    databaseFields = new[] { "MapName (string)", "X (int)", "Y (int)", "Direction (int)", "Type (script|shop|warp)", "DisplayName (string)", "SpriteID (int)" },
                    clientPairing = "SpriteID maps to client sprite identifiers in NPC identity lua tables. Hashtag suffixes hide duplicate names.",
                    validationRules = new[] { "Duplicate NPC names without hashtag suffixes (e.g. John#01) cause script override warning/errors." }
                };
                break;

            case "map":
                schema = new
                {
                    entityType = "map",
                    primaryFields = new[] { "MapName" },
                    databaseFields = new[] { "MapName (string, max 12 chars)", "NumericalIndex (int)" },
                    clientPairing = "Requires the core map trio (.rsw, .gnd, .gat) in client GRF containers or loose data/ directory.",
                    validationRules = new[] { "Server registration in maps_athena.conf and map_index.txt must match client trio.", "Missing .gat is a critical passability crash blocker." }
                };
                break;

            case "asset":
                schema = new
                {
                    entityType = "asset",
                    primaryFields = new[] { "FilePath" },
                    databaseFields = new[] { "FilePath (string)", "Encoding (EUC-KR)", "Container (GRF/loose)" },
                    clientPairing = "Sprites require paired .spr (pixels/palette) and .act (anchors/animations) under the same directory.",
                    validationRules = new[] { "Loose .spr/.act missing counterpart is flagged as a broken asset glitch.", "Path casing is normalized case-insensitive." }
                };
                break;

            default:
                return JsonOutput.Error("knowledge-schema", "Unsupported entity type for schema. Supported: item, equipment, mob, npc, map, asset.");
        }

        var output = JsonOutput.Success("knowledge-schema", $"Schema reference loaded for: '{entityType}'");
        output.Data = new { entityType, schema };
        return output;
    }

    private static void AddIndexWarning(KnowledgeService service, JsonOutput output)
    {
        if (!string.IsNullOrWhiteSpace(service.LastReadOnlyIndexWarning))
        {
            output.Warnings.Add(service.LastReadOnlyIndexWarning);
        }
    }

    private static string? ValidateInput(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return $"Missing required argument: --{name}";

        if (value.Length > maxLength)
            return $"Invalid {name}: maximum length is {maxLength} characters.";

        if (value.Any(char.IsControl))
            return $"Invalid {name}: control characters are blocked.";

        if (LooksPathLike(value))
            return $"Invalid {name}: path-like input is blocked for this command.";

        return null;
    }

    private static bool LooksPathLike(string value) =>
        value.Contains("..", StringComparison.Ordinal) ||
        value.Contains(':', StringComparison.Ordinal) ||
        value.Contains('\\', StringComparison.Ordinal) ||
        value.Contains('/', StringComparison.Ordinal);
}
