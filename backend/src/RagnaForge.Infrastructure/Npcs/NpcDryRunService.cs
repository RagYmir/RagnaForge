using System.Text.RegularExpressions;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Items;
using RagnaForge.Domain.Npcs;
using RagnaForge.Infrastructure.FileSystem;
using RagnaForge.Infrastructure.Items;

namespace RagnaForge.Infrastructure.Npcs;

public sealed class NpcDryRunService(
    IGrfAssetLookupService? grfAssetLookupService = null,
    GrfAssetLookupOptions? grfAssetLookupOptions = null)
{
    private readonly IGrfAssetLookupService? _grfAssetLookupService = grfAssetLookupService;
    private readonly GrfAssetLookupOptions _grfAssetLookupOptions = grfAssetLookupService is null
        ? GrfAssetLookupOptions.Disabled
        : grfAssetLookupOptions ?? GrfAssetLookupOptions.Disabled;

    public NpcDryRunReport Create(RepositoryPaths paths, EpisodeProfile episodeProfile, NpcDefinitionInput input)
    {
        var serverDependencies = new List<ItemDependency>();
        var dependencies = new List<ItemDependency>();
        var proposedChanges = new List<ProposedFileChange>();
        var warnings = new List<string>();

        var scriptTarget = SafeFileSystem.Combine(paths.RathenaPath, "npc", "custom", BuildFileName(input));
        var scriptsCustomPath = SafeFileSystem.Combine(paths.RathenaPath, "npc", "scripts_custom.conf");

        if (File.Exists(scriptTarget))
        {
            serverDependencies.Add(new ItemDependency("NPC", ItemDependencyState.Missing, "Target NPC script file already exists.", scriptTarget));
        }
        else
        {
            serverDependencies.Add(new ItemDependency("NPC", ItemDependencyState.Satisfied, "Target NPC script file is free.", scriptTarget));
        }

        if (!File.Exists(scriptsCustomPath))
        {
            serverDependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Missing, "npc/scripts_custom.conf was not found.", scriptsCustomPath));
        }
        else
        {
            serverDependencies.Add(new ItemDependency("rAthena", ItemDependencyState.Satisfied, "npc/scripts_custom.conf is available.", scriptsCustomPath));
        }

        if (!MapExists(paths.RathenaPath, input.MapName))
        {
            serverDependencies.Add(new ItemDependency("Map", ItemDependencyState.Missing, $"Map '{input.MapName}' was not found in rAthena map registration files."));
        }
        else
        {
            serverDependencies.Add(new ItemDependency("Map", ItemDependencyState.Satisfied, $"Map '{input.MapName}' is registered in rAthena."));
        }

        if (!LooseMapExists(paths.PatchPath, input.MapName))
        {
            serverDependencies.Add(new ItemDependency("Patch", ItemDependencyState.Warning, $"Loose patch map files for '{input.MapName}' were not found; client may still rely on GRF assets."));
        }
        else
        {
            serverDependencies.Add(new ItemDependency("Patch", ItemDependencyState.Satisfied, $"Loose patch map files for '{input.MapName}' are present."));
        }

        dependencies.AddRange(serverDependencies);

        var loaderLine = $"npc: npc/custom/{BuildFileName(input)}";
        if (File.Exists(scriptsCustomPath)
            && File.ReadAllText(scriptsCustomPath).Contains(loaderLine, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"scripts_custom.conf already references {BuildFileName(input)}.");
        }
        else
        {
            proposedChanges.Add(new ProposedFileChange(
                scriptsCustomPath,
                "append",
                File.Exists(scriptsCustomPath),
                loaderLine));
        }

        proposedChanges.Add(new ProposedFileChange(
            scriptTarget,
            "create",
            File.Exists(scriptTarget),
            BuildNpcScript(input)));

        var clientIdentityPlanner = new NpcClientIdentityPlanner(_grfAssetLookupService, _grfAssetLookupOptions);
        var clientIdentity = clientIdentityPlanner.Create(paths, input);
        dependencies.AddRange(clientIdentity.Dependencies);
        proposedChanges.AddRange(clientIdentity.ProposedChanges);
        warnings.AddRange(clientIdentity.Plan.ValidationWarnings);

        dependencies.Add(new ItemDependency("DryRun", ItemDependencyState.Proposed, $"Prepared {proposedChanges.Count} proposed file change(s) for NPC diff preview."));

        var serverCanApply = serverDependencies.All(item => item.State != ItemDependencyState.Missing);
        var canApply = serverCanApply && (!clientIdentity.Plan.Required || clientIdentity.Plan.CanApply);
        var applyReadiness = canApply
            ? NpcApplyReadiness.Ready
            : serverCanApply
                ? NpcApplyReadiness.ReadyServerOnly
                : NpcApplyReadiness.Blocked;
        var diffPreview = ItemDiffPreviewBuilder.Build(proposedChanges);

        return new NpcDryRunReport(
            DateTimeOffset.UtcNow,
            episodeProfile,
            input,
            canApply,
            dependencies,
            proposedChanges,
            diffPreview,
            warnings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            clientIdentity.SpriteValidation,
            clientIdentity.SpriteResolution,
            serverCanApply,
            applyReadiness,
            clientIdentity.Plan);
    }

    private static string BuildNpcScript(NpcDefinitionInput input)
    {
        var body = string.IsNullOrWhiteSpace(input.ScriptBody)
            ? "mes \"RagnaForge NPC preview.\";\n\tclose;"
            : input.ScriptBody!.Replace("\\n", "\n", StringComparison.Ordinal);

        return $"{input.MapName},{input.X},{input.Y},{input.Direction}\tscript\t{input.Name}\t{input.Sprite},{{\n\t{body.Replace("\n", "\n\t", StringComparison.Ordinal)}\n}}";
    }

    private static string BuildFileName(NpcDefinitionInput input)
    {
        var slug = string.IsNullOrWhiteSpace(input.FileSlug) ? input.Name : input.FileSlug!;
        slug = Regex.Replace(slug.Trim().ToLowerInvariant(), @"[^a-z0-9_]+", "_");
        slug = slug.Trim('_');
        return $"ragnaforge_npc_{(slug.Length == 0 ? "custom" : slug)}.txt";
    }

    private static bool MapExists(string rathenaPath, string mapName)
    {
        var mapIndexPath = SafeFileSystem.Combine(rathenaPath, "db", "import", "map_index.txt");
        var mapsAthenaPath = SafeFileSystem.Combine(rathenaPath, "conf", "maps_athena.conf");

        return SafeFileSystem.ReadLinesIfExists(mapIndexPath).Any(line => line.TrimStart().StartsWith(mapName + " ", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals(mapName, StringComparison.OrdinalIgnoreCase))
               || SafeFileSystem.ReadLinesIfExists(mapsAthenaPath).Any(line => line.Trim().Equals($"map: {mapName}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooseMapExists(string patchPath, string mapName)
    {
        var dataPath = SafeFileSystem.Combine(patchPath, "data");
        return File.Exists(SafeFileSystem.Combine(dataPath, mapName + ".rsw"))
               && File.Exists(SafeFileSystem.Combine(dataPath, mapName + ".gnd"))
               && File.Exists(SafeFileSystem.Combine(dataPath, mapName + ".gat"));
    }
}
