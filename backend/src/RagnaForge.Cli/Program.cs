using System.Text.Json;
using System.Text.Json.Serialization;
using RagnaForge.Application.Abstractions;
using RagnaForge.Application.Configuration;
using RagnaForge.Application.Discovery;
using RagnaForge.Application.Grf;
using RagnaForge.Application.Visuals;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Maps;
using RagnaForge.Domain.Monsters;
using RagnaForge.Domain.Npcs;
using RagnaForge.Domain.Visuals;
using RagnaForge.Infrastructure.Configuration;
using RagnaForge.Infrastructure.Grf;
using RagnaForge.Infrastructure.GrfEditorIntegration;
using RagnaForge.Infrastructure.Items;
using RagnaForge.Infrastructure.Maps;
using RagnaForge.Infrastructure.Monsters;
using RagnaForge.Infrastructure.Npcs;
using RagnaForge.Infrastructure.Patch;
using RagnaForge.Infrastructure.Rathena;
using RagnaForge.Infrastructure.Visuals;

if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return 0;
}

try
{
    var command = args[0].Trim().ToLowerInvariant();
    return command switch
    {
        "config" => RunConfig(args.Skip(1).ToArray()),
        "discover" => RunDiscover(args.Skip(1).ToArray()),
        "grf" => RunGrf(args.Skip(1).ToArray()),
        "item" => RunItem(args.Skip(1).ToArray()),
        "equipment" => RunEquipment(args.Skip(1).ToArray()),
        "npc" => RunNpc(args.Skip(1).ToArray()),
        "monster" or "mob" => RunMonster(args.Skip(1).ToArray()),
        "map" => RunMap(args.Skip(1).ToArray()),
        "visual-equipment-themes" or "visual-themes" or "visuals" => RunVisualThemes(args.Skip(1).ToArray()),
        _ => UnknownCommand(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int RunConfig(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing config subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());
    var store = CreateManifestStore();
    var validator = new ConfigurationManifestValidator();

    return subcommand switch
    {
        "init" => RunConfigInit(values, store, validator),
        "validate" => RunConfigValidate(values, store, validator),
        _ => UnknownCommand("config " + args[0])
    };
}

static int RunConfigInit(
    Dictionary<string, string> values,
    JsonConfigurationManifestStore store,
    ConfigurationManifestValidator validator)
{
    var missing = Required(values, "rathena", "patch", "grfs", "grf-editor");
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing required arguments: " + string.Join(", ", missing.Select(item => "--" + item)));
        PrintUsage();
        return 2;
    }

    var profile = new EpisodeProfile(
        values.GetValueOrDefault("episode-name", "progressive-current"),
        ParseMode(values.GetValueOrDefault("episode-mode", "pre-renewal")),
        EmptyToNull(values.GetValueOrDefault("client-date")),
        values.GetValueOrDefault("episode-notes", "Progressive server profile; mode may change in future episodes."));

    var manifest = ConfigurationManifest.Create(
        new RepositoryPaths(
            values["rathena"],
            values["patch"],
            values["grfs"],
            values["grf-editor"]),
        profile,
        ParseBool(values.GetValueOrDefault("progressive"), defaultValue: true),
        notes:
        [
            "Local manifest for repository paths only.",
            "rAthena, Patch/client and GRF repositories remain the source of truth.",
            "Current episode is treated as Pre-Renewal until build/log evidence says otherwise."
        ]);

    var manifestPath = values.GetValueOrDefault("out", store.DefaultManifestPath);
    store.Save(manifestPath, manifest, ParseBool(values.GetValueOrDefault("force"), defaultValue: false));

    var validation = validator.Validate(manifest);
    WriteJson(new
    {
        ManifestPath = Path.GetFullPath(manifestPath),
        Validation = validation,
        Manifest = manifest
    });

    return validation.IsValid ? 0 : 1;
}

static int RunConfigValidate(
    Dictionary<string, string> values,
    JsonConfigurationManifestStore store,
    ConfigurationManifestValidator validator)
{
    var manifestPath = values.GetValueOrDefault("config", store.DefaultManifestPath);
    var manifest = store.Load(manifestPath);
    var validation = validator.Validate(manifest);

    WriteJson(new
    {
        ManifestPath = Path.GetFullPath(manifestPath),
        Validation = validation
    });

    return validation.IsValid ? 0 : 1;
}

static int RunDiscover(string[] args)
{
    var values = ParseArgs(args);
    var options = values.TryGetValue("config", out var configPath)
        ? CreateDiscoveryOptionsFromManifest(values, configPath)
        : CreateDiscoveryOptionsFromArguments(values);

    var report = CreateDiscoveryService().Run(options);
    WriteJson(report);
    return 0;
}

static int RunGrf(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing grf subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());

    return subcommand switch
    {
        "index" => RunGrfIndex(values),
        "inspect" => RunGrfInspect(values),
        _ => UnknownCommand("grf " + args[0])
    };
}

static int RunItem(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing item subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());

    return subcommand switch
    {
        "dry-run" or "dryrun" => RunItemDryRun(values),
        "diff-preview" or "diffpreview" => RunItemDiffPreview(values),
        "apply" => RunItemApply(values),
        "rollback" => RunItemRollback(values),
        _ => UnknownCommand("item " + args[0])
    };
}

static int RunEquipment(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing equipment subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());

    return subcommand switch
    {
        "dry-run" or "dryrun" => RunEquipmentDryRun(values),
        "diff-preview" or "diffpreview" => RunEquipmentDiffPreview(values),
        "apply" => RunEquipmentApply(values),
        "rollback" => RunEquipmentRollback(values),
        _ => UnknownCommand("equipment " + args[0])
    };
}

static int RunNpc(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing npc subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());

    return subcommand switch
    {
        "dry-run" or "dryrun" => RunNpcDryRun(values),
        "diff-preview" or "diffpreview" => RunNpcDiffPreview(values),
        "apply" => RunNpcApply(values),
        "rollback" => RunNpcRollback(values),
        _ => UnknownCommand("npc " + args[0])
    };
}

static int RunMonster(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing monster subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());

    return subcommand switch
    {
        "dry-run" or "dryrun" => RunMonsterDryRun(values),
        "diff-preview" or "diffpreview" => RunMonsterDiffPreview(values),
        "apply" => RunMonsterApply(values),
        "rollback" => RunMonsterRollback(values),
        _ => UnknownCommand("monster " + args[0])
    };
}

static int RunMap(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing map subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());

    return subcommand switch
    {
        "dry-run" or "dryrun" => RunMapDryRun(values),
        "diff-preview" or "diffpreview" => RunMapDiffPreview(values),
        "apply" => RunMapApply(values),
        "rollback" => RunMapRollback(values),
        _ => UnknownCommand("map " + args[0])
    };
}

static int RunItemDryRun(Dictionary<string, string> values)
{
    var report = BuildItemDryRunReport(values);
    WriteJson(report);
    return report.CanApply ? 0 : 1;
}

static int RunItemDiffPreview(Dictionary<string, string> values)
{
    var report = BuildItemDryRunReport(values);
    WriteJson(report.DiffPreview);
    return report.CanApply ? 0 : 1;
}

static int RunItemApply(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "APPLY", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm APPLY");
        return 2;
    }

    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var report = BuildItemDryRunReport(values);
    var result = new LegacyItemApplyService(Directory.GetCurrentDirectory()).Apply(manifest.Paths, report);
    WriteJson(result);
    return result.Applied ? 0 : 1;
}

static int RunItemRollback(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "ROLLBACK", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm ROLLBACK");
        return 2;
    }

    if (!values.TryGetValue("log", out var logPath) || string.IsNullOrWhiteSpace(logPath))
    {
        Console.Error.WriteLine("Missing required argument: --log");
        return 2;
    }

    var result = new LegacyItemApplyService(Directory.GetCurrentDirectory()).Rollback(logPath);
    WriteJson(result);
    return result.RolledBack ? 0 : 1;
}

static int RunEquipmentDryRun(Dictionary<string, string> values)
{
    var report = BuildEquipmentDryRunReport(values);
    WriteJson(report);
    return report.CanApply ? 0 : 1;
}

static int RunEquipmentDiffPreview(Dictionary<string, string> values)
{
    var report = BuildEquipmentDryRunReport(values);
    WriteJson(report.DiffPreview);
    return report.CanApply ? 0 : 1;
}

static int RunEquipmentApply(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "APPLY", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm APPLY");
        return 2;
    }

    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var report = BuildEquipmentDryRunReport(values);
    var result = new LegacyEquipmentApplyService(Directory.GetCurrentDirectory()).Apply(manifest.Paths, report);
    WriteJson(result);
    return result.Applied ? 0 : 1;
}

static int RunEquipmentRollback(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "ROLLBACK", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm ROLLBACK");
        return 2;
    }

    if (!values.TryGetValue("log", out var logPath) || string.IsNullOrWhiteSpace(logPath))
    {
        Console.Error.WriteLine("Missing required argument: --log");
        return 2;
    }

    var result = new LegacyEquipmentApplyService(Directory.GetCurrentDirectory()).Rollback(logPath);
    WriteJson(result);
    return result.RolledBack ? 0 : 1;
}

static int RunNpcDryRun(Dictionary<string, string> values)
{
    var report = BuildNpcDryRunReport(values);
    WriteJson(report);
    return report.CanApply ? 0 : 1;
}

static int RunNpcDiffPreview(Dictionary<string, string> values)
{
    var report = BuildNpcDryRunReport(values);
    WriteJson(new
    {
        report.DiffPreview,
        report.SpriteResolution,
        report.SpriteValidation.DetectionSource,
        report.ClientIdentityRequired,
        report.ClientIdentityPlan,
        report.RequiredClientFiles,
        report.ExistingClientRegistration,
        report.ExistingClientRegistrationDetails,
        report.ProposedClientRegistration,
        report.BytecodeBlocks,
        report.CanApplyClientIdentity,
        report.ServerCanApply,
        report.ApplyReadiness,
        report.CanApply
    });
    return report.CanApply ? 0 : 1;
}

static int RunNpcApply(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "APPLY", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm APPLY");
        return 2;
    }

    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var report = BuildNpcDryRunReport(values);
    var result = new NpcApplyService(Directory.GetCurrentDirectory()).Apply(
        manifest.Paths,
        report,
        values.TryGetValue("allow-server-only", out var rawAllowServerOnly) && ParseBool(rawAllowServerOnly, defaultValue: false));
    WriteJson(result);
    return result.Applied ? 0 : 1;
}

static int RunNpcRollback(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "ROLLBACK", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm ROLLBACK");
        return 2;
    }

    if (!values.TryGetValue("log", out var logPath) || string.IsNullOrWhiteSpace(logPath))
    {
        Console.Error.WriteLine("Missing required argument: --log");
        return 2;
    }

    var result = new NpcApplyService(Directory.GetCurrentDirectory()).Rollback(logPath);
    WriteJson(result);
    return result.RolledBack ? 0 : 1;
}

static int RunMonsterDryRun(Dictionary<string, string> values)
{
    var report = BuildMonsterDryRunReport(values);
    WriteJson(report);
    return report.CanApply ? 0 : 1;
}

static int RunMonsterDiffPreview(Dictionary<string, string> values)
{
    var report = BuildMonsterDryRunReport(values);
    WriteJson(new
    {
        report.DiffPreview,
        report.Drops,
        report.Skills,
        report.Spawns,
        report.UnsupportedFields,
        report.ValidationWarnings,
        report.ValidationErrors,
        report.ApplyReadiness,
        report.PostWriteValidationPlan,
        report.CanApply
    });
    return report.CanApply ? 0 : 1;
}

static int RunMonsterApply(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "APPLY", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm APPLY");
        return 2;
    }

    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var report = BuildMonsterDryRunReport(values);
    var result = new MonsterApplyService(Directory.GetCurrentDirectory()).Apply(manifest.Paths, report);
    WriteJson(result);
    return result.Applied ? 0 : 1;
}

static int RunMonsterRollback(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "ROLLBACK", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm ROLLBACK");
        return 2;
    }

    if (!values.TryGetValue("log", out var logPath) || string.IsNullOrWhiteSpace(logPath))
    {
        Console.Error.WriteLine("Missing required argument: --log");
        return 2;
    }

    var result = new MonsterApplyService(Directory.GetCurrentDirectory()).Rollback(logPath);
    WriteJson(result);
    return result.RolledBack ? 0 : 1;
}

static int RunMapDryRun(Dictionary<string, string> values)
{
    var report = BuildMapDryRunReport(values);
    WriteJson(report);
    return report.CanApply ? 0 : 1;
}

static int RunMapDiffPreview(Dictionary<string, string> values)
{
    var report = BuildMapDryRunReport(values);
    WriteJson(report.DiffPreview);
    return report.CanApply ? 0 : 1;
}

static int RunMapApply(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "APPLY", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm APPLY");
        return 2;
    }

    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var report = BuildMapDryRunReport(values);
    var result = new MapApplyService(Directory.GetCurrentDirectory()).Apply(manifest.Paths, report);
    WriteJson(result);
    return result.Applied ? 0 : 1;
}

static int RunMapRollback(Dictionary<string, string> values)
{
    if (!values.TryGetValue("confirm", out var confirmValue) || !string.Equals(confirmValue, "ROLLBACK", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Missing required confirmation: --confirm ROLLBACK");
        return 2;
    }

    if (!values.TryGetValue("log", out var logPath) || string.IsNullOrWhiteSpace(logPath))
    {
        Console.Error.WriteLine("Missing required argument: --log");
        return 2;
    }

    var result = new MapApplyService(Directory.GetCurrentDirectory()).Rollback(logPath);
    WriteJson(result);
    return result.RolledBack ? 0 : 1;
}

static int RunVisualThemes(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Missing visual-equipment-themes subcommand.");
        PrintUsage();
        return 2;
    }

    var subcommand = args[0].Trim().ToLowerInvariant();
    var values = ParseArgs(args.Skip(1).ToArray());
    var store = CreateVisualThemeStore();
    var validator = new VisualThemeManifestValidator();

    return subcommand switch
    {
        "init" => RunVisualThemesInit(values, store, validator),
        "validate" => RunVisualThemesValidate(values, store, validator),
        "list" => RunVisualThemesList(values, store, validator),
        _ => UnknownCommand("visual-equipment-themes " + args[0])
    };
}

static int RunVisualThemesInit(
    Dictionary<string, string> values,
    JsonVisualThemeManifestStore store,
    VisualThemeManifestValidator validator)
{
    var manifest = VisualThemeManifest.CreateDefault();
    var manifestPath = values.GetValueOrDefault("out", store.DefaultManifestPath);
    store.Save(manifestPath, manifest, ParseBool(values.GetValueOrDefault("force"), defaultValue: false));
    var validation = validator.Validate(manifest);

    WriteJson(new
    {
        ManifestPath = Path.GetFullPath(manifestPath),
        Validation = validation,
        Manifest = manifest
    });

    return validation.IsValid ? 0 : 1;
}

static int RunVisualThemesValidate(
    Dictionary<string, string> values,
    JsonVisualThemeManifestStore store,
    VisualThemeManifestValidator validator)
{
    var manifestPath = values.GetValueOrDefault("config", store.DefaultManifestPath);
    var manifest = store.Load(manifestPath);
    var validation = validator.Validate(manifest);

    WriteJson(new
    {
        ManifestPath = Path.GetFullPath(manifestPath),
        Validation = validation
    });

    return validation.IsValid ? 0 : 1;
}

static int RunVisualThemesList(
    Dictionary<string, string> values,
    JsonVisualThemeManifestStore store,
    VisualThemeManifestValidator validator)
{
    var manifestPath = values.GetValueOrDefault("config", store.DefaultManifestPath);
    var manifest = store.Load(manifestPath);
    var validation = validator.Validate(manifest);

    WriteJson(new
    {
        ManifestPath = Path.GetFullPath(manifestPath),
        Validation = validation,
        Themes = manifest.Themes
            .OrderBy(theme => theme.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray()
    });

    return validation.IsValid ? 0 : 1;
}

static int RunGrfIndex(Dictionary<string, string> values)
{
    var indexStore = new JsonGrfRepositoryIndexStore(Directory.GetCurrentDirectory());
    var rootPath = values.TryGetValue("config", out var configPath)
        ? LoadValidatedManifest(configPath).Paths.GrfRepositoryPath
        : values.GetValueOrDefault("grfs");

    if (string.IsNullOrWhiteSpace(rootPath))
    {
        Console.Error.WriteLine("Missing required argument: --config or --grfs");
        PrintUsage();
        return 2;
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    var result = new CachedGrfRepositoryIndexer(indexStore).Build(
        new GrfRepositoryIndexOptions(
            rootPath,
            values.GetValueOrDefault("cache", indexStore.DefaultIndexPath),
            ParseInt(values.GetValueOrDefault("max-containers"), defaultValue: 200),
            ParseBool(values.GetValueOrDefault("force"), defaultValue: false),
            !ParseBool(values.GetValueOrDefault("no-save"), defaultValue: false)),
        cancellation.Token);

    WriteJson(result);
    return 0;
}

static int RunGrfInspect(Dictionary<string, string> values)
{
    if (!values.TryGetValue("container", out var requestedContainer) || string.IsNullOrWhiteSpace(requestedContainer))
    {
        Console.Error.WriteLine("Missing required argument: --container");
        PrintUsage();
        return 2;
    }

    ConfigurationManifest? manifest = null;
    if (values.TryGetValue("config", out var configPath))
    {
        manifest = LoadValidatedManifest(configPath);
    }

    var grfEditorPath = manifest?.Paths.GrfEditorPath ?? values.GetValueOrDefault("grf-editor");
    if (string.IsNullOrWhiteSpace(grfEditorPath))
    {
        Console.Error.WriteLine("Missing required argument: --config or --grf-editor");
        PrintUsage();
        return 2;
    }

    var containerPath = ResolveContainerPath(manifest?.Paths.GrfRepositoryPath, requestedContainer);
    var store = new JsonGrfContainerIndexStore(Directory.GetCurrentDirectory());
    var result = new GrfAssemblyContainerInspector().Inspect(
        grfEditorPath,
        containerPath,
        ParseInt(values.GetValueOrDefault("limit"), defaultValue: 200));

    var shouldSave = !ParseBool(values.GetValueOrDefault("no-save"), defaultValue: false);
    var indexPath = values.GetValueOrDefault("cache", store.BuildDefaultIndexPath(containerPath));

    if (shouldSave)
    {
        store.Save(indexPath, result.Index, ParseBool(values.GetValueOrDefault("force"), defaultValue: false));
    }

    WriteJson(new
    {
        IndexPath = shouldSave ? Path.GetFullPath(indexPath) : null,
        Saved = shouldSave,
        Result = result
    });

    return 0;
}

static DiscoveryOptions CreateDiscoveryOptionsFromManifest(Dictionary<string, string> values, string configPath)
{
    var manifest = LoadValidatedManifest(configPath);

    return new DiscoveryOptions(
        manifest.Paths,
        manifest.EpisodeProfile,
        ParseInt(values.GetValueOrDefault("max-grf-containers"), defaultValue: 200));
}

static ConfigurationManifest LoadValidatedManifest(string configPath)
{
    var store = CreateManifestStore();
    var validator = new ConfigurationManifestValidator();
    var manifest = store.Load(configPath);
    var validation = validator.Validate(manifest);

    if (!validation.IsValid)
    {
        WriteJson(new
        {
            ManifestPath = Path.GetFullPath(configPath),
            Validation = validation
        });

        throw new InvalidOperationException("Manifest validation failed.");
    }

    return manifest;
}

static DiscoveryOptions CreateDiscoveryOptionsFromArguments(Dictionary<string, string> values)
{
    var missing = Required(values, "rathena", "patch", "grfs", "grf-editor");
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing required arguments: " + string.Join(", ", missing.Select(item => "--" + item)));
        PrintUsage();
        Environment.ExitCode = 2;
        throw new InvalidOperationException("Missing required discovery arguments.");
    }

    var profile = new EpisodeProfile(
        values.GetValueOrDefault("episode-name", "current-progressive-episode"),
        ParseMode(values.GetValueOrDefault("episode-mode", "unknown")),
        EmptyToNull(values.GetValueOrDefault("client-date")),
        values.GetValueOrDefault("episode-notes", "Progressive server profile; mode may change in future episodes."));

    return new DiscoveryOptions(
        new RepositoryPaths(
            values["rathena"],
            values["patch"],
            values["grfs"],
            values["grf-editor"]),
        profile,
        ParseInt(values.GetValueOrDefault("max-grf-containers"), defaultValue: 200));
}

static RepositoryDiscoveryService CreateDiscoveryService() =>
    new(
        new RathenaScanner(),
        new PatchScanner(),
        new GrfRepositoryScanner(),
        new GrfEditorProbe());

static JsonConfigurationManifestStore CreateManifestStore() =>
    new(Directory.GetCurrentDirectory());

static JsonVisualThemeManifestStore CreateVisualThemeStore() =>
    new(Directory.GetCurrentDirectory());

static IGrfAssetLookupService CreateGrfAssetLookupService() =>
    new IndexedGrfAssetLookupService(
        new JsonGrfContainerIndexStore(Directory.GetCurrentDirectory()),
        new GrfAssemblyAssetLookupService());

static Dictionary<string, string> ParseArgs(string[] values)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < values.Length; index++)
    {
        var token = values[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = token[2..];
        if (index + 1 >= values.Length || values[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = "true";
            continue;
        }

        result[key] = values[index + 1];
        index++;
    }

    return result;
}

static string[] Required(Dictionary<string, string> values, params string[] keys) =>
    keys.Where(key => !values.ContainsKey(key) || string.IsNullOrWhiteSpace(values[key])).ToArray();

static EpisodeMode ParseMode(string? value) =>
    value?.Trim().ToLowerInvariant() switch
    {
        "pre" or "pre-renewal" or "prerenewal" => EpisodeMode.PreRenewal,
        "re" or "renewal" => EpisodeMode.Renewal,
        "hybrid" or "hibrido" => EpisodeMode.Hybrid,
        _ => EpisodeMode.Unknown
    };

static int ParseInt(string? value, int defaultValue) =>
    int.TryParse(value, out var parsed) ? parsed : defaultValue;

static bool ParseBool(string? value, bool defaultValue) =>
    string.IsNullOrWhiteSpace(value)
        ? defaultValue
        : value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
          || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
          || value.Trim().Equals("sim", StringComparison.OrdinalIgnoreCase)
          || value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);

static string? EmptyToNull(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value;

static IReadOnlyList<string> SplitPipeLines(string? value) =>
    string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static IReadOnlyList<string> SplitList(string? value) =>
    string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(['|', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static IReadOnlyList<Dictionary<string, string>> ParseStructuredEntries(string? value) =>
    string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseStructuredEntry)
            .ToArray();

static Dictionary<string, string> ParseStructuredEntry(string value)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var segment in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var separatorIndex = segment.IndexOf('=');
        if (separatorIndex <= 0)
        {
            result[segment.Trim()] = "true";
            continue;
        }

        var key = segment[..separatorIndex].Trim();
        var parsedValue = segment[(separatorIndex + 1)..].Trim();
        if (key.Length > 0)
        {
            result[key] = parsedValue;
        }
    }

    return result;
}

static GrfAssetLookupOptions BuildGrfAssetLookupOptions(Dictionary<string, string> values, string grfRepositoryPath)
{
    var explicitContainers = SplitList(values.GetValueOrDefault("asset-grf-container"))
        .Select(container => ResolveContainerPath(grfRepositoryPath, container))
        .ToArray();
    var enabled = ParseBool(values.GetValueOrDefault("scan-grf-assets"), defaultValue: false)
                  || explicitContainers.Length > 0;
    var maxContainers = ParseInt(values.GetValueOrDefault("max-grf-asset-containers"), defaultValue: explicitContainers.Length > 0 ? explicitContainers.Length : 1);
    var maxMatches = ParseInt(values.GetValueOrDefault("max-grf-asset-matches"), defaultValue: 10);

    if (!enabled)
    {
        return GrfAssetLookupOptions.Disabled;
    }

    var containers = explicitContainers.Length > 0
        ? explicitContainers
        : LoadGrfAssetContainersFromRepositoryCache(values, maxContainers);

    return new GrfAssetLookupOptions(
        true,
        containers,
        Math.Max(1, maxContainers),
        Math.Max(1, maxMatches));
}

static IReadOnlyList<string> LoadGrfAssetContainersFromRepositoryCache(Dictionary<string, string> values, int maxContainers)
{
    var store = new JsonGrfRepositoryIndexStore(Directory.GetCurrentDirectory());
    var cachePath = values.GetValueOrDefault("grf-cache", store.DefaultIndexPath);
    var document = store.TryLoad(cachePath);
    if (document is null)
    {
        return [];
    }

    return document.Containers
        .Where(container => container.Extension.Equals(".grf", StringComparison.OrdinalIgnoreCase)
                            || container.Extension.Equals(".gpf", StringComparison.OrdinalIgnoreCase))
        .Take(Math.Max(1, maxContainers))
        .Select(container => container.FullPath)
        .ToArray();
}

static ItemDryRunReport BuildItemDryRunReport(Dictionary<string, string> values)
{
    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var input = BuildItemDefinitionInput(values);

    var grfLookupOptions = BuildGrfAssetLookupOptions(values, manifest.Paths.GrfRepositoryPath);
    var dryRunService = grfLookupOptions.Enabled
        ? new LegacyItemDryRunService(CreateGrfAssetLookupService(), grfLookupOptions)
        : new LegacyItemDryRunService();

    return dryRunService.Create(manifest.Paths, manifest.EpisodeProfile, input);
}

static EquipmentDryRunReport BuildEquipmentDryRunReport(Dictionary<string, string> values)
{
    var itemReportValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
    {
        ["type"] = values.GetValueOrDefault("type", "Armor")
    };

    if (values.TryGetValue("slots", out var slots))
    {
        itemReportValues["slots"] = slots;
    }

    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var itemInput = BuildItemDefinitionInput(itemReportValues);
    var input = new EquipmentDefinitionInput(
        itemInput,
        SplitList(values.GetValueOrDefault("locations")),
        EmptyToNull(values.GetValueOrDefault("visual-category")),
        values.TryGetValue("view", out var rawView) && int.TryParse(rawView, out var parsedView) ? parsedView : null,
        EmptyToNull(values.GetValueOrDefault("client-symbol")),
        EmptyToNull(values.GetValueOrDefault("client-sprite")),
        SplitList(values.GetValueOrDefault("jobs")),
        EmptyToNull(values.GetValueOrDefault("gender")),
        values.TryGetValue("equip-level-min", out var rawEquipLevelMin) && int.TryParse(rawEquipLevelMin, out var parsedEquipLevelMin) ? parsedEquipLevelMin : null,
        values.TryGetValue("equip-level-max", out var rawEquipLevelMax) && int.TryParse(rawEquipLevelMax, out var parsedEquipLevelMax) ? parsedEquipLevelMax : null,
        values.TryGetValue("armor-level", out var rawArmorLevel) && int.TryParse(rawArmorLevel, out var parsedArmorLevel) ? parsedArmorLevel : null,
        values.TryGetValue("weapon-level", out var rawWeaponLevel) && int.TryParse(rawWeaponLevel, out var parsedWeaponLevel) ? parsedWeaponLevel : null,
        values.TryGetValue("defense", out var rawDefense) && int.TryParse(rawDefense, out var parsedDefense) ? parsedDefense : null,
        values.TryGetValue("refineable", out var rawRefineable) ? ParseBool(rawRefineable, defaultValue: false) : null,
        EmptyToNull(values.GetValueOrDefault("equip-script")),
        EmptyToNull(values.GetValueOrDefault("unequip-script")),
        EmptyToNull(values.GetValueOrDefault("weapon-base-type")),
        EmptyToNull(values.GetValueOrDefault("weapon-hit-sound")));

    var grfLookupOptions = BuildGrfAssetLookupOptions(values, manifest.Paths.GrfRepositoryPath);
    var service = grfLookupOptions.Enabled
        ? new LegacyEquipmentDryRunService(CreateGrfAssetLookupService(), grfLookupOptions)
        : new LegacyEquipmentDryRunService();
    var visualTheme = TryResolveVisualThemeEvaluation(values, input);

    var report = service.Create(manifest.Paths, manifest.EpisodeProfile, input, visualTheme);
    return ApplyVisualThemeEvaluation(report, values);
}

static NpcDryRunReport BuildNpcDryRunReport(Dictionary<string, string> values)
{
    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var input = BuildNpcDefinitionInput(values);
    var grfLookupOptions = BuildGrfAssetLookupOptions(values, manifest.Paths.GrfRepositoryPath);
    var service = grfLookupOptions.Enabled
        ? new NpcDryRunService(CreateGrfAssetLookupService(), grfLookupOptions)
        : new NpcDryRunService();
    return service.Create(manifest.Paths, manifest.EpisodeProfile, input);
}

static MonsterDryRunReport BuildMonsterDryRunReport(Dictionary<string, string> values)
{
    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var input = BuildMonsterDefinitionInput(values);
    return new MonsterDryRunService().Create(manifest.Paths, manifest.EpisodeProfile, input);
}

static MapDryRunReport BuildMapDryRunReport(Dictionary<string, string> values)
{
    var manifestPath = values.GetValueOrDefault("config", CreateManifestStore().DefaultManifestPath);
    var manifest = LoadValidatedManifest(manifestPath);
    var input = BuildMapDeploymentInput(values);
    var grfLookupOptions = BuildGrfAssetLookupOptions(values, manifest.Paths.GrfRepositoryPath);
    var service = grfLookupOptions.Enabled
        ? new MapDryRunService(
            CreateGrfAssetLookupService(),
            grfLookupOptions,
            new GrfAssemblyFileExtractor(),
            Path.Combine(Directory.GetCurrentDirectory(), "tmp", "map-dependency-scan"))
        : new MapDryRunService();
    return service.Create(manifest.Paths, manifest.EpisodeProfile, input);
}

static VisualThemeEvaluation? TryResolveVisualThemeEvaluation(
    Dictionary<string, string> values,
    EquipmentDefinitionInput input)
{
    var store = CreateVisualThemeStore();
    var manifestPath = values.GetValueOrDefault("visual-theme-config", store.DefaultManifestPath);
    var fullManifestPath = Path.GetFullPath(manifestPath);
    var requestedTheme = EmptyToNull(values.GetValueOrDefault("visual-theme"));
    var shouldInspectThemeManifest =
        !string.IsNullOrWhiteSpace(requestedTheme)
        || values.ContainsKey("visual-theme-config")
        || File.Exists(fullManifestPath);

    if (!shouldInspectThemeManifest)
    {
        return null;
    }

    try
    {
        var manifest = store.Load(manifestPath);
        var validation = new VisualThemeManifestValidator().Validate(manifest);
        if (!validation.IsValid)
        {
            return null;
        }

        return new VisualEquipmentThemeMatcher().Evaluate(
            manifest,
            input,
            fullManifestPath,
            requestedTheme,
            ParseInt(values.GetValueOrDefault("visual-theme-limit"), defaultValue: 3));
    }
    catch
    {
        return null;
    }
}

static EquipmentDryRunReport ApplyVisualThemeEvaluation(
    EquipmentDryRunReport report,
    Dictionary<string, string> values)
{
    var store = CreateVisualThemeStore();
    var manifestPath = values.GetValueOrDefault("visual-theme-config", store.DefaultManifestPath);
    var fullManifestPath = Path.GetFullPath(manifestPath);
    var requestedTheme = EmptyToNull(values.GetValueOrDefault("visual-theme"));
    var shouldInspectThemeManifest =
        !string.IsNullOrWhiteSpace(requestedTheme)
        || values.ContainsKey("visual-theme-config")
        || File.Exists(fullManifestPath);

    if (!shouldInspectThemeManifest)
    {
        return report;
    }

    var dependencies = report.Dependencies.ToList();
    var warnings = report.Warnings.ToList();

    try
    {
        var manifest = store.Load(manifestPath);
        var validation = new VisualThemeManifestValidator().Validate(manifest);
        if (!validation.IsValid)
        {
            dependencies.Add(new ItemDependency(
                "VisualTheme",
                ItemDependencyState.Warning,
                $"Visual equipment theme manifest is invalid with {validation.Issues.Count} issue(s).",
                fullManifestPath));

            foreach (var issue in validation.Issues)
            {
                warnings.Add($"{issue.Code}: {issue.Message}");
            }

            return report with
            {
                Dependencies = dependencies,
                Warnings = warnings
            };
        }

        var evaluation = new VisualEquipmentThemeMatcher().Evaluate(
            manifest,
            report.Input,
            fullManifestPath,
            requestedTheme,
            ParseInt(values.GetValueOrDefault("visual-theme-limit"), defaultValue: 3));

        if (evaluation.SelectedTheme is not null)
        {
            dependencies.Add(new ItemDependency(
                "VisualTheme",
                ItemDependencyState.Satisfied,
                BuildVisualThemeSelectionMessage(evaluation.SelectedTheme),
                fullManifestPath));
        }
        else if (!string.IsNullOrWhiteSpace(evaluation.RequestedKey))
        {
            dependencies.Add(new ItemDependency(
                "VisualTheme",
                ItemDependencyState.Warning,
                $"Requested visual theme '{evaluation.RequestedKey}' could not be resolved from the local manifest.",
                fullManifestPath));
        }
        else if (evaluation.SuggestedThemes.Count > 0)
        {
            warnings.Add("Suggested visual themes: " + string.Join(", ", evaluation.SuggestedThemes.Select(FormatVisualThemeSuggestion)));
            dependencies.Add(new ItemDependency(
                "VisualTheme",
                ItemDependencyState.Warning,
                "Suggested visual themes: " + string.Join(", ", evaluation.SuggestedThemes.Select(FormatVisualThemeSuggestion)) + ".",
                fullManifestPath));
        }

        foreach (var issue in evaluation.Issues)
        {
            dependencies.Add(new ItemDependency(
                "VisualTheme",
                ItemDependencyState.Warning,
                issue,
                fullManifestPath));
            warnings.Add(issue);
        }

        return report with
        {
            Dependencies = dependencies,
            Warnings = warnings,
            VisualTheme = evaluation
        };
    }
    catch (Exception ex)
    {
        dependencies.Add(new ItemDependency(
            "VisualTheme",
            ItemDependencyState.Warning,
            $"Visual equipment theme manifest could not be loaded: {ex.Message}",
            fullManifestPath));
        warnings.Add($"Visual theme manifest could not be loaded: {ex.Message}");

        return report with
        {
            Dependencies = dependencies,
            Warnings = warnings
        };
    }
}

static ItemDefinitionInput BuildItemDefinitionInput(Dictionary<string, string> values)
{
    var missing = Required(values, "aegis", "name");
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing required arguments: " + string.Join(", ", missing.Select(item => "--" + item)));
        PrintUsage();
        Environment.ExitCode = 2;
        throw new InvalidOperationException("Missing required item arguments.");
    }

    return new ItemDefinitionInput(
        values.TryGetValue("id", out var rawId) && int.TryParse(rawId, out var parsedId) ? parsedId : null,
        values["aegis"],
        values["name"],
        values.GetValueOrDefault("resource", values["aegis"]),
        values.GetValueOrDefault("type", "Etc"),
        ParseInt(values.GetValueOrDefault("buy"), defaultValue: 0),
        ParseInt(values.GetValueOrDefault("sell"), defaultValue: 0),
        ParseInt(values.GetValueOrDefault("weight"), defaultValue: 0),
        ParseInt(values.GetValueOrDefault("slots"), defaultValue: 0),
        EmptyToNull(values.GetValueOrDefault("script")),
        SplitPipeLines(values.GetValueOrDefault("identified-desc")),
        EmptyToNull(values.GetValueOrDefault("unidentified-name")),
        EmptyToNull(values.GetValueOrDefault("unidentified-resource")),
        SplitPipeLines(values.GetValueOrDefault("unidentified-desc")));
}

static NpcDefinitionInput BuildNpcDefinitionInput(Dictionary<string, string> values)
{
    var missing = Required(values, "name", "map", "x", "y");
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing required arguments: " + string.Join(", ", missing.Select(item => "--" + item)));
        PrintUsage();
        Environment.ExitCode = 2;
        throw new InvalidOperationException("Missing required NPC arguments.");
    }

    return new NpcDefinitionInput(
        values["name"],
        values["map"],
        ParseInt(values["x"], 0),
        ParseInt(values["y"], 0),
        ParseInt(values.GetValueOrDefault("dir"), 2),
        values.GetValueOrDefault("sprite", "4_M_JOB_BLACKSMITH"),
        EmptyToNull(values.GetValueOrDefault("script-body")),
        EmptyToNull(values.GetValueOrDefault("file-slug")),
        EmptyToNull(values.GetValueOrDefault("client-symbol")),
        values.TryGetValue("client-id", out var rawClientId) ? ParseInt(rawClientId, 0) : null);
}

static MonsterDefinitionInput BuildMonsterDefinitionInput(Dictionary<string, string> values)
{
    var missing = Required(values, "aegis", "name", "map");
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing required arguments: " + string.Join(", ", missing.Select(item => "--" + item)));
        PrintUsage();
        Environment.ExitCode = 2;
        throw new InvalidOperationException("Missing required monster arguments.");
    }

    var primarySpawn = new MonsterSpawnDefinition(
        ParseInt(values.GetValueOrDefault("spawn-x"), 0),
        ParseInt(values.GetValueOrDefault("spawn-y"), 0),
        ParseInt(values.GetValueOrDefault("spawn-area-x"), 0),
        ParseInt(values.GetValueOrDefault("spawn-area-y"), 0),
        EmptyToNull(values.GetValueOrDefault("spawn-label")),
        values["map"],
        ParseInt(values.GetValueOrDefault("amount"), 1),
        ParseInt(values.GetValueOrDefault("respawn"), 60000),
        EmptyToNull(values.GetValueOrDefault("spawn-event")),
        values.TryGetValue("spawn-random", out var rawSpawnRandom) && ParseBool(rawSpawnRandom, defaultValue: false));

    var primarySkill = values.TryGetValue("skill-id", out var rawSkillId) && int.TryParse(rawSkillId, out var parsedSkillId)
        ? new MonsterSkillDefinition(
            parsedSkillId,
            ParseInt(values.GetValueOrDefault("skill-lv"), 1),
            values.GetValueOrDefault("skill-state", "any"),
            ParseInt(values.GetValueOrDefault("skill-rate"), 10000),
            ParseInt(values.GetValueOrDefault("skill-casttime"), 0),
            ParseInt(values.GetValueOrDefault("skill-delay"), 5000),
            values.TryGetValue("skill-cancelable", out var rawSkillCancelable) && ParseBool(rawSkillCancelable, defaultValue: false),
            values.GetValueOrDefault("skill-target", "target"),
            values.GetValueOrDefault("skill-cond-type", "always"),
            ParseInt(values.GetValueOrDefault("skill-cond-value"), 0),
            values.TryGetValue("skill-val1", out var rawSkillVal1) && int.TryParse(rawSkillVal1, out var parsedSkillVal1) ? parsedSkillVal1 : null,
            values.TryGetValue("skill-val2", out var rawSkillVal2) && int.TryParse(rawSkillVal2, out var parsedSkillVal2) ? parsedSkillVal2 : null,
            values.TryGetValue("skill-val3", out var rawSkillVal3) && int.TryParse(rawSkillVal3, out var parsedSkillVal3) ? parsedSkillVal3 : null,
            values.TryGetValue("skill-val4", out var rawSkillVal4) && int.TryParse(rawSkillVal4, out var parsedSkillVal4) ? parsedSkillVal4 : null,
            values.TryGetValue("skill-val5", out var rawSkillVal5) && int.TryParse(rawSkillVal5, out var parsedSkillVal5) ? parsedSkillVal5 : null,
            values.TryGetValue("skill-emotion", out var rawSkillEmotion) && int.TryParse(rawSkillEmotion, out var parsedSkillEmotion) ? parsedSkillEmotion : null,
            EmptyToNull(values.GetValueOrDefault("skill-chat")),
            EmptyToNull(values.GetValueOrDefault("skill-anchor")))
        : null;

    var drops = new List<MonsterDropDefinition>();
    if (values.TryGetValue("drop-id", out var rawDropId) && int.TryParse(rawDropId, out var parsedDropId)
        || values.TryGetValue("drop-aegis", out _)
        || values.TryGetValue("drop-item", out _))
    {
        var dropItemToken = values.GetValueOrDefault("drop-item");
        int? dropId = values.TryGetValue("drop-id", out rawDropId) && int.TryParse(rawDropId, out parsedDropId)
            ? parsedDropId
            : int.TryParse(dropItemToken, out var parsedFromToken) ? parsedFromToken : null;
        var dropAegis = values.GetValueOrDefault("drop-aegis");
        if (string.IsNullOrWhiteSpace(dropAegis) && !string.IsNullOrWhiteSpace(dropItemToken) && !int.TryParse(dropItemToken, out _))
        {
            dropAegis = dropItemToken;
        }

        drops.Add(new MonsterDropDefinition(
            dropId,
            EmptyToNull(dropAegis),
            ParseInt(values.GetValueOrDefault("drop-chance"), 100),
            values.TryGetValue("drop-quantity", out var rawDropQuantity) && int.TryParse(rawDropQuantity, out var parsedDropQuantity) ? parsedDropQuantity : null,
            values.TryGetValue("drop-mvp", out var rawDropMvp) && ParseBool(rawDropMvp, defaultValue: false),
            EmptyToNull(values.GetValueOrDefault("drop-kind"))));
    }

    foreach (var entry in ParseStructuredEntries(values.GetValueOrDefault("drops")))
    {
        var itemToken = entry.GetValueOrDefault("item") ?? entry.GetValueOrDefault("aegis");
        int? dropId = entry.TryGetValue("id", out var rawStructuredDropId) && int.TryParse(rawStructuredDropId, out var parsedStructuredDropId)
            ? parsedStructuredDropId
            : int.TryParse(itemToken, out var parsedItemToken) ? parsedItemToken : null;
        var dropAegis = entry.GetValueOrDefault("aegis");
        if (string.IsNullOrWhiteSpace(dropAegis) && !string.IsNullOrWhiteSpace(itemToken) && !int.TryParse(itemToken, out _))
        {
            dropAegis = itemToken;
        }

        drops.Add(new MonsterDropDefinition(
            dropId,
            EmptyToNull(dropAegis),
            ParseInt(entry.GetValueOrDefault("chance"), 100),
            entry.TryGetValue("quantity", out var rawStructuredQuantity) && int.TryParse(rawStructuredQuantity, out var parsedStructuredQuantity) ? parsedStructuredQuantity : null,
            entry.TryGetValue("mvp", out var rawStructuredMvp) && ParseBool(rawStructuredMvp, defaultValue: false),
            EmptyToNull(entry.GetValueOrDefault("kind"))));
    }

    var skills = ParseStructuredEntries(values.GetValueOrDefault("skills"))
        .Select(entry =>
        {
            var unsupported = entry
                .Where(pair => pair.Key is not ("id" or "lv" or "level" or "state" or "rate" or "cast" or "casttime" or "delay" or "cancelable" or "target" or "condition" or "conditiontype" or "condtype" or "conditionvalue" or "condvalue" or "val1" or "val2" or "val3" or "val4" or "val5" or "emotion" or "chat" or "anchor"))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            return new MonsterSkillDefinition(
                ParseInt(entry.GetValueOrDefault("id"), 0),
                ParseInt(entry.GetValueOrDefault("lv") ?? entry.GetValueOrDefault("level"), 1),
                entry.GetValueOrDefault("state", "any"),
                ParseInt(entry.GetValueOrDefault("rate"), 10000),
                ParseInt(entry.GetValueOrDefault("cast") ?? entry.GetValueOrDefault("casttime"), 0),
                ParseInt(entry.GetValueOrDefault("delay"), 5000),
                entry.TryGetValue("cancelable", out var rawStructuredCancelable) && ParseBool(rawStructuredCancelable, defaultValue: false),
                entry.GetValueOrDefault("target", "target"),
                entry.GetValueOrDefault("condition") ?? entry.GetValueOrDefault("conditiontype") ?? entry.GetValueOrDefault("condtype") ?? "always",
                ParseInt(entry.GetValueOrDefault("conditionvalue") ?? entry.GetValueOrDefault("condvalue"), 0),
                entry.TryGetValue("val1", out var rawStructuredVal1) && int.TryParse(rawStructuredVal1, out var parsedStructuredVal1) ? parsedStructuredVal1 : null,
                entry.TryGetValue("val2", out var rawStructuredVal2) && int.TryParse(rawStructuredVal2, out var parsedStructuredVal2) ? parsedStructuredVal2 : null,
                entry.TryGetValue("val3", out var rawStructuredVal3) && int.TryParse(rawStructuredVal3, out var parsedStructuredVal3) ? parsedStructuredVal3 : null,
                entry.TryGetValue("val4", out var rawStructuredVal4) && int.TryParse(rawStructuredVal4, out var parsedStructuredVal4) ? parsedStructuredVal4 : null,
                entry.TryGetValue("val5", out var rawStructuredVal5) && int.TryParse(rawStructuredVal5, out var parsedStructuredVal5) ? parsedStructuredVal5 : null,
                entry.TryGetValue("emotion", out var rawStructuredEmotion) && int.TryParse(rawStructuredEmotion, out var parsedStructuredEmotion) ? parsedStructuredEmotion : null,
                EmptyToNull(entry.GetValueOrDefault("chat")),
                EmptyToNull(entry.GetValueOrDefault("anchor")),
                unsupported.Count == 0 ? null : unsupported);
        })
        .ToArray();

    var spawns = ParseStructuredEntries(values.GetValueOrDefault("spawns"))
        .Select(entry => new MonsterSpawnDefinition(
            ParseInt(entry.GetValueOrDefault("x"), 0),
            ParseInt(entry.GetValueOrDefault("y"), 0),
            ParseInt(entry.GetValueOrDefault("areax"), 0),
            ParseInt(entry.GetValueOrDefault("areay"), 0),
            EmptyToNull(entry.GetValueOrDefault("label")),
            EmptyToNull(entry.GetValueOrDefault("map")) ?? values["map"],
            ParseInt(entry.GetValueOrDefault("amount"), ParseInt(values.GetValueOrDefault("amount"), 1)),
            ParseInt(entry.GetValueOrDefault("respawn"), ParseInt(values.GetValueOrDefault("respawn"), 60000)),
            EmptyToNull(entry.GetValueOrDefault("event")),
            entry.TryGetValue("random", out var rawStructuredRandom) && ParseBool(rawStructuredRandom, defaultValue: false)))
        .ToArray();

    return new MonsterDefinitionInput(
        values.TryGetValue("id", out var rawId) && int.TryParse(rawId, out var parsedId) ? parsedId : null,
        values["aegis"],
        values["name"],
        values["map"],
        ParseInt(values.GetValueOrDefault("level"), 1),
        ParseInt(values.GetValueOrDefault("hp"), 10),
        ParseInt(values.GetValueOrDefault("amount"), 1),
        ParseInt(values.GetValueOrDefault("respawn"), 60000),
        EmptyToNull(values.GetValueOrDefault("sprite")),
        EmptyToNull(values.GetValueOrDefault("file-slug")),
        primarySpawn,
        primarySkill,
        drops,
        skills,
        spawns,
        values.TryGetValue("allow-future-drop-references", out var rawAllowFutureDropReferences) && ParseBool(rawAllowFutureDropReferences, defaultValue: false));
}

static MapDeploymentInput BuildMapDeploymentInput(Dictionary<string, string> values)
{
    var missing = Required(values, "map-name");
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing required arguments: " + string.Join(", ", missing.Select(item => "--" + item)));
        PrintUsage();
        Environment.ExitCode = 2;
        throw new InvalidOperationException("Missing required map arguments.");
    }

    return new MapDeploymentInput(
        values["map-name"],
        EmptyToNull(values.GetValueOrDefault("rsw-resource")) ?? values["map-name"],
        EmptyToNull(values.GetValueOrDefault("gnd-resource")) ?? values["map-name"],
        EmptyToNull(values.GetValueOrDefault("gat-resource")) ?? values["map-name"],
        EmptyToNull(values.GetValueOrDefault("file-slug")));
}

static string ResolveContainerPath(string? repositoryRoot, string requestedContainer)
{
    if (Path.IsPathRooted(requestedContainer))
    {
        return requestedContainer;
    }

    if (!string.IsNullOrWhiteSpace(repositoryRoot))
    {
        return Path.Combine(repositoryRoot, requestedContainer);
    }

    return requestedContainer;
}

static void WriteJson<T>(T value)
{
    Console.WriteLine(JsonSerializer.Serialize(
        value,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage();
    return 2;
}

static void PrintUsage()
{
    Console.WriteLine("""
RagnaForge CLI

Usage:
  dotnet run --project backend/src/RagnaForge.Cli -- config init ^
    --rathena "<RATHENA_PATH>" ^
    --patch "<PATCH_PATH>" ^
    --grfs "<GRF_REPOSITORY_PATH>" ^
    --grf-editor "<GRF_EDITOR_PATH>" ^
    --episode-name "progressive-current" ^
    --episode-mode pre-renewal

  dotnet run --project backend/src/RagnaForge.Cli -- config validate ^
    --config data\manifests\repositories.local.json

  dotnet run --project backend/src/RagnaForge.Cli -- visual-equipment-themes init ^
    --out data\manifests\visual-equipment-themes.local.json

  dotnet run --project backend/src/RagnaForge.Cli -- visual-equipment-themes list ^
    --config data\manifests\visual-equipment-themes.local.json

  dotnet run --project backend/src/RagnaForge.Cli -- discover ^
    --config data\manifests\repositories.local.json ^
    --max-grf-containers 20

  dotnet run --project backend/src/RagnaForge.Cli -- discover ^
    --rathena "<RATHENA_PATH>" ^
    --patch "<PATCH_PATH>" ^
    --grfs "<GRF_REPOSITORY_PATH>" ^
    --grf-editor "<GRF_EDITOR_PATH>"

  dotnet run --project backend/src/RagnaForge.Cli -- grf index ^
    --config data\manifests\repositories.local.json ^
    --cache data\cache\grf-repository.index.json ^
    --max-containers 200

  dotnet run --project backend/src/RagnaForge.Cli -- grf inspect ^
    --config data\manifests\repositories.local.json ^
    --container sample.grf ^
    --cache data\indexes\sample.index.json ^
    --limit 200

  dotnet run --project backend/src/RagnaForge.Cli -- item dry-run ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Test_Item ^
    --name "RagnaForge Test Item" ^
    --resource RF_Test_Item ^
    --type Etc ^
    --buy 10 ^
    --sell 5 ^
    --weight 10 ^
    --identified-desc "Linha 1|Linha 2"

  dotnet run --project backend/src/RagnaForge.Cli -- item dry-run ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Dragon_Ring ^
    --name "RagnaForge Dragon Ring" ^
    --resource dragon_ring ^
    --asset-grf-container data_0.grf

  dotnet run --project backend/src/RagnaForge.Cli -- item diff-preview ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Test_Item ^
    --name "RagnaForge Test Item" ^
    --resource RF_Test_Item

  dotnet run --project backend/src/RagnaForge.Cli -- item apply ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Test_Item ^
    --name "RagnaForge Test Item" ^
    --resource RF_Test_Item ^
    --confirm APPLY

  dotnet run --project backend/src/RagnaForge.Cli -- item rollback ^
    --log data\logs\items\20260507-000000-item-apply-example.apply.json ^
    --confirm ROLLBACK

  dotnet run --project backend/src/RagnaForge.Cli -- equipment dry-run ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Costume_Rabbit ^
    --name "RagnaForge Costume Rabbit" ^
    --resource c_rabbit_winged_robe ^
    --type Armor ^
    --locations Head_Top ^
    --visual-category headgear ^
    --view 5000 ^
    --client-symbol ACCESSORY_RF_COSTUME_RABBIT ^
    --client-sprite _rf_costume_rabbit ^
    --visual-theme fofo ^
    --defense 3 ^
    --equip-level-min 10 ^
    --refineable true

  dotnet run --project backend/src/RagnaForge.Cli -- equipment diff-preview ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Test_Sword ^
    --name "RagnaForge Test Sword" ^
    --resource rf_test_sword ^
    --type Weapon ^
    --locations Right_Hand ^
    --visual-category weapon ^
    --view 6000 ^
    --client-symbol WEAPONTYPE_RF_TEST_SWORD ^
    --client-sprite _rf_test_sword ^
    --weapon-base-type SWORD ^
    --weapon-level 3 ^
    --refineable true

  dotnet run --project backend/src/RagnaForge.Cli -- equipment apply ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_Costume_Rabbit ^
    --name "RagnaForge Costume Rabbit" ^
    --resource c_rabbit_winged_robe ^
    --type Armor ^
    --locations Head_Top ^
    --visual-category headgear ^
    --view 5000 ^
    --client-symbol ACCESSORY_RF_COSTUME_RABBIT ^
    --client-sprite _rf_costume_rabbit ^
    --confirm APPLY

  dotnet run --project backend/src/RagnaForge.Cli -- equipment rollback ^
    --log data\logs\equipment\20260511-000000-equipment-apply-example.apply.json ^
    --confirm ROLLBACK

  dotnet run --project backend/src/RagnaForge.Cli -- npc diff-preview ^
    --config data\manifests\repositories.local.json ^
    --name "RagnaForge Guide" ^
    --map prontera ^
    --x 150 ^
    --y 180 ^
    --dir 2 ^
    --sprite 4_M_JOB_BLACKSMITH

  dotnet run --project backend/src/RagnaForge.Cli -- npc dry-run ^
    --config data\manifests\repositories.local.json ^
    --name "Custom Guide" ^
    --map prontera ^
    --x 150 ^
    --y 180 ^
    --sprite CustomGuide ^
    --client-symbol JT_CUSTOM_GUIDE ^
    --client-id 5000

  dotnet run --project backend/src/RagnaForge.Cli -- npc apply ^
    --config data\manifests\repositories.local.json ^
    --name "RagnaForge Guide" ^
    --map prontera ^
    --x 150 ^
    --y 180 ^
    --dir 2 ^
    --sprite 4_M_JOB_BLACKSMITH ^
    --confirm APPLY

  dotnet run --project backend/src/RagnaForge.Cli -- npc apply ^
    --config data\manifests\repositories.local.json ^
    --name "Custom Guide" ^
    --map prontera ^
    --x 150 ^
    --y 180 ^
    --sprite CustomGuide ^
    --client-symbol JT_CUSTOM_GUIDE ^
    --client-id 5000 ^
    --allow-server-only true ^
    --confirm APPLY

  dotnet run --project backend/src/RagnaForge.Cli -- npc rollback ^
    --log data\logs\npcs\20260510-000000-npc-apply-example.apply.json ^
    --confirm ROLLBACK

  dotnet run --project backend/src/RagnaForge.Cli -- monster diff-preview ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_TEST_MOB ^
    --name "RagnaForge Test Mob" ^
    --map prontera ^
    --level 10 ^
    --hp 1000 ^
    --amount 5 ^
    --respawn 60000

  dotnet run --project backend/src/RagnaForge.Cli -- monster diff-preview ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_TEST_MOB ^
    --name "RagnaForge Test Mob" ^
    --map prontera ^
    --level 10 ^
    --hp 1000 ^
    --amount 5 ^
    --respawn 60000 ^
    --spawn-x 120 ^
    --spawn-y 140 ^
    --spawn-area-x 8 ^
    --spawn-area-y 6 ^
    --spawn-label "RF Test Spawn" ^
    --skill-id 175 ^
    --skill-lv 1 ^
    --skill-target self ^
    --skill-cond-type always

  dotnet run --project backend/src/RagnaForge.Cli -- monster apply ^
    --config data\manifests\repositories.local.json ^
    --aegis RF_TEST_MOB ^
    --name "RagnaForge Test Mob" ^
    --map prontera ^
    --level 10 ^
    --hp 1000 ^
    --amount 5 ^
    --respawn 60000 ^
    --confirm APPLY

  dotnet run --project backend/src/RagnaForge.Cli -- monster rollback ^
    --log data\logs\monsters\20260511-000000-monster-apply-example.apply.json ^
    --confirm ROLLBACK

  dotnet run --project backend/src/RagnaForge.Cli -- map diff-preview ^
    --config data\manifests\repositories.local.json ^
    --map-name sample ^
    --asset-grf-container data_0.grf

  dotnet run --project backend/src/RagnaForge.Cli -- map apply ^
    --config data\manifests\repositories.local.json ^
    --map-name sample ^
    --asset-grf-container data_0.grf ^
    --confirm APPLY

  dotnet run --project backend/src/RagnaForge.Cli -- map rollback ^
    --log data\logs\maps\20260511-000000-map-apply-example.apply.json ^
    --confirm ROLLBACK

This milestone keeps apply behind explicit confirmation. Item, equipment, NPC, monster and map apply/rollback write logs/backups inside data\logs and data\backups.
""");
}

static string BuildVisualThemeSelectionMessage(VisualThemeSuggestion suggestion)
{
    var parts = new List<string>
    {
        $"Visual theme '{suggestion.DisplayName}' selected with score {suggestion.Score}"
    };

    if (suggestion.MatchedCategories.Count > 0)
    {
        parts.Add("categories: " + string.Join(", ", suggestion.MatchedCategories));
    }

    if (suggestion.MatchedEquipLocations.Count > 0)
    {
        parts.Add("equip locations: " + string.Join(", ", suggestion.MatchedEquipLocations));
    }

    if (suggestion.MatchedPatterns.Count > 0)
    {
        parts.Add("patterns: " + string.Join(", ", suggestion.MatchedPatterns));
    }

    return string.Join("; ", parts) + ".";
}

static string FormatVisualThemeSuggestion(VisualThemeSuggestion suggestion) =>
    $"{suggestion.Key} (score {suggestion.Score})";
