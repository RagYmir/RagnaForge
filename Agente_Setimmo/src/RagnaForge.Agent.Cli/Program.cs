using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Logging;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Runtime;

namespace RagnaForge.Agent.Cli;

/// <summary>
/// Agente Setimmo CLI entry point.
/// All commands return JSON. Fatal errors also return JSON.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex)
        {
            Console.WriteLine(JsonOutput.Fatal(ex.Message).ToJson());
            return 2;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0) { PrintUsage(); return 1; }

        var command = args[0].ToLowerInvariant();

        if (command is "--help" or "-h" or "help") { PrintUsage(); return 0; }
        if (command is "--version" or "version")
        {
            Console.WriteLine($"{{\"version\":\"{RagnaForge.Agent.Core.AgentVersion.Current}\",\"name\":\"Agente Setimmo\"}}");
            return 0;
        }

        var resolution = AgentRootResolver.Resolve(AppContext.BaseDirectory);
        var agentRoot = resolution.AgentRoot;
        var configDir = Path.Combine(agentRoot, "config");

        if (!resolution.ConfigExists)
            return EmitWithoutLog(new JsonOutput
            {
                Ok = false,
                Mode = command,
                Summary = "RagnaForge agentRoot could not be resolved.",
                Errors =
                [
                    $"Missing required config file: {Path.Combine(configDir, "paths.json")}",
                    $"Set {AgentRootResolver.EnvironmentVariable} to the Agente Setimmo root or run scripts/install.ps1 again."
                ],
                NextRequiredAction = "configure_agent_root",
                SafeForAutomation = false,
                Data = new { attemptedAgentRoot = agentRoot, resolutionSource = resolution.Source }
            });

        switch (command)
        {
            case "status": return Emit(agentRoot, new StatusCommand(configDir).Execute(), "status");
            case "doctor": return Emit(agentRoot, new DoctorCommand(configDir).Execute(), "doctor");
            case "baseline": return Emit(agentRoot, new BaselineCommand(configDir, agentRoot).Execute(), "baseline");
            case "health": return Emit(agentRoot, new HealthCommand(configDir, agentRoot).Execute(), "health");

            case "scan":
                if (!HasFlag(args, "--project"))
                    return Emit(agentRoot, JsonOutput.Error("scan",
                        "Missing required argument: --project. Usage: ragnaforge scan --project --json"), "scan");
                return Emit(agentRoot, new ScanCommand(configDir, agentRoot).Execute(), "scan");

            case "config":
                return RunConfig(args, configDir, agentRoot);
            case "profile":
                return RunProfile(args, configDir, agentRoot);
            case "index":
                return RunIndex(args, configDir, agentRoot);
            case "find":
                return RunFind(args, configDir, agentRoot);
            case "validate":
                return RunValidate(args, configDir, agentRoot);
            case "triage":
                return RunTriage(args, configDir, agentRoot);
            case "dry-run":
                return RunDryRun(args, configDir, agentRoot);
            case "diff":
                return RunDiff(args, agentRoot);
            case "report":
                return RunReport(args, agentRoot);
            case "rollback":
                return RunRollback(args, agentRoot);
            case "knowledge":
                return RunKnowledge(args, configDir, agentRoot);

            case "apply":
                return Emit(agentRoot, new JsonOutput
                {
                    Ok = false, Mode = "apply",
                    Summary = "Real apply is blocked in this MVP.",
                    SafeForAutomation = false, NextRequiredAction = "blocked_by_safety_policy"
                }, "apply");

            default:
                return Emit(agentRoot, JsonOutput.Error("unknown", $"Unknown command: {command}"), "unknown");
        }
    }

    private static int RunConfig(string[] args, string configDir, string agentRoot)
    {
        var sub = GetArg(args, 1) ?? "";
        var key = GetArg(args, 2);
        var value = GetArg(args, 3);
        return Emit(agentRoot, new ConfigCommand(configDir, agentRoot, sub, key, value).Execute(), "config");
    }

    private static int RunProfile(string[] args, string configDir, string agentRoot)
    {
        var sub = GetArg(args, 1) ?? "";
        var name = GetArg(args, 2);
        return Emit(agentRoot, new ProfileCommand(configDir, agentRoot, sub, name).Execute(), "profile");
    }

    private static int RunIndex(string[] args, string configDir, string agentRoot)
    {
        var scope = "entities";
        if (HasFlag(args, "--items")) scope = "items";
        else if (HasFlag(args, "--npcs")) scope = "npcs";
        else if (HasFlag(args, "--monsters")) scope = "monsters";
        else if (HasFlag(args, "--maps")) scope = "maps";
        else if (HasFlag(args, "--entities")) scope = "entities";

        return Emit(agentRoot, new IndexCommand(configDir, agentRoot, scope).Execute(), "index");
    }

    private static int RunFind(string[] args, string configDir, string agentRoot)
    {
        var entityType = GetArg(args, 1); // item, npc, monster, map
        if (string.IsNullOrWhiteSpace(entityType))
            return Emit(agentRoot, JsonOutput.Error("find",
                "Usage: ragnaforge find <item|npc|monster|map> --id <id> | --name <name>"), "find");

        int? id = null;
        string? name = null;

        var idVal = GetFlagValue(args, "--id");
        if (idVal != null && int.TryParse(idVal, out var parsedId)) id = parsedId;
        name = GetFlagValue(args, "--name");

        if (id is null && name is null)
            return Emit(agentRoot, JsonOutput.Error("find", "Specify --id or --name."), "find");

        return Emit(agentRoot, new FindCommand(configDir, agentRoot, entityType, id, name).Execute(), "find");
    }

    private static int RunValidate(string[] args, string configDir, string agentRoot)
    {
        var scope = "all";
        if (HasFlag(args, "--items")) scope = "items";
        else if (HasFlag(args, "--npcs")) scope = "npcs";
        else if (HasFlag(args, "--monsters")) scope = "monsters";
        else if (HasFlag(args, "--maps")) scope = "maps";
        else if (HasFlag(args, "--client")) scope = "client";
        else if (HasFlag(args, "--server")) scope = "server";

        return Emit(agentRoot, new ValidateCommand(configDir, agentRoot, scope).Execute(), "validate");
    }

    private static int RunTriage(string[] args, string configDir, string agentRoot)
    {
        var externalOnly = HasFlag(args, "--external-data");
        return Emit(agentRoot, new TriageCommand(configDir, agentRoot, externalOnly).Execute(), "triage");
    }

    private static int RunDryRun(string[] args, string configDir, string agentRoot)
    {
        var entityType = GetArg(args, 1);
        var inputPath = GetFlagValue(args, "--input");

        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(inputPath))
            return Emit(agentRoot, JsonOutput.Error("dry-run",
                "Usage: ragnaforge dry-run <item|npc|monster|map> --input <file.json>"), "dry-run");

        return Emit(agentRoot, new DryRunCommand(configDir, agentRoot, entityType, inputPath).Execute(), "dry-run");
    }

    private static int RunDiff(string[] args, string agentRoot)
    {
        var last = HasFlag(args, "--last");
        var opId = GetFlagValue(args, "--operation");

        if (!last && opId is null)
            return Emit(agentRoot, JsonOutput.Error("diff",
                "Usage: ragnaforge diff --last | --operation <id>"), "diff");

        return Emit(agentRoot, new DiffCommand(agentRoot, opId, last).Execute(), "diff");
    }

    private static int RunReport(string[] args, string agentRoot)
    {
        var last = HasFlag(args, "--last");
        var opId = GetFlagValue(args, "--operation");
        var format = GetFlagValue(args, "--format") ?? "json";

        if (!last && opId is null)
            return Emit(agentRoot, JsonOutput.Error("report",
                "Usage: ragnaforge report --last | --operation <id> --format json|md"), "report");

        return Emit(agentRoot, new ReportCommand(agentRoot, opId, last, format).Execute(), "report");
    }

    private static int RunRollback(string[] args, string agentRoot)
    {
        var list = HasFlag(args, "--list");
        var id = GetFlagValue(args, "--id");
        var dryRun = HasFlag(args, "--dry-run");
        var confirm = HasFlag(args, "--confirm");

        return Emit(agentRoot, new RollbackCommand(agentRoot, id, list, dryRun, confirm).Execute(), "rollback");
    }

    private static int RunKnowledge(string[] args, string configDir, string agentRoot)
    {
        var sub = GetArg(args, 1) ?? "search";
        var dict = new Dictionary<string, string>();

        var query = GetFlagValue(args, "--query");
        if (query != null) dict["query"] = query;

        var topic = GetFlagValue(args, "--topic");
        if (topic != null) dict["topic"] = topic;

        var id = GetFlagValue(args, "--id");
        if (id != null) dict["id"] = id;

        var entity = GetFlagValue(args, "--entity");
        if (entity != null) dict["entity"] = entity;

        return Emit(agentRoot, new KnowledgeCommand(configDir, agentRoot, sub, dict).Execute(), "knowledge");
    }

    // --- Helpers ---

    private static int Emit(string agentRoot, JsonOutput result, string operation)
    {
        LogResult(agentRoot, result, operation);
        Console.WriteLine(result.ToJson());
        return result.Ok ? 0 : 1;
    }

    private static int EmitWithoutLog(JsonOutput result)
    {
        Console.WriteLine(result.ToJson());
        return result.Ok ? 0 : 1;
    }

    private static void LogResult(string agentRoot, JsonOutput result, string operation)
    {
        try
        {
            var logger = new AgentLogger(agentRoot);
            logger.EnsureDirectories();
            logger.LogAgent(result.OperationId, result.ActiveProfile ?? "unknown",
                result.ConfigFingerprint ?? "unknown", operation,
                result.Ok ? "success" : "failure", result.Warnings, result.Errors);
        }
        catch { }
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetArg(string[] args, int index) =>
        index < args.Length && !args[index].StartsWith("--") ? args[index] : null;

    private static string? GetFlagValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Agente Setimmo CLI

        Usage: ragnaforge <command> [options]

        Diagnostics:
          status                  Show agent status (read-only)
          doctor                  Validate configuration and security (read-only)
          baseline                Run status + doctor + scan + index + validate
          health                  Return compact operational health summary
          scan --project          Scan and index project files (read-only)

        Config & Profiles:
          config get              Show current configuration
          config validate         Validate configuration safety
          config set <key> <val>  Set a configuration value
          profile list            List available profiles
          profile use <name>      Switch active profile
          profile validate        Validate active profile

        Entity Indexing:
          index --entities        Index all entities (items, NPCs, monsters, maps)
          index --items           Index items only
          index --npcs            Index NPCs only
          index --monsters        Index monsters only
          index --maps            Index maps only

        Search:
          find item --id <id>     Find item by ID
          find item --name <n>    Find item by name
          find npc --name <n>     Find NPC by name
          find monster --id <id>  Find monster by ID
          find monster --name <n> Find monster by name
          find map --name <n>     Find map by name

        Validation:
          validate                Validate all entities
          validate --items        Validate items only
          validate --npcs         Validate NPCs only
          validate --monsters     Validate monsters only
          validate --maps         Validate maps only
          triage                  Triage external validation issues (read-only)

        Planning:
          dry-run <type> --input <file.json>  Plan changes (no apply)
          diff --last                         View last operation diff
          diff --operation <id>               View operation diff
          report --last --format json|md      Generate report
          report --operation <id> --format json|md

        Rollback (informational):
          rollback --list                     List rollback plans
          rollback --id <id> --dry-run        Preview rollback

        Options:
          --json    Output in JSON format (default)
          version   Show version
          help      Show this help
        """);
    }
}
