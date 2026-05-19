using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Scanning;

namespace RagnaForge.Agent.Core.Commands;

public sealed class HealthCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;

    public HealthCommand(string configDir, string agentRoot)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("health");

        try
        {
            var loader = new ConfigLoader(_configDir);
            var pathsConfig = loader.LoadPathsConfig();
            var safetyConfig = loader.LoadSafetyConfig();
            var profile = ConfigLoader.GetActiveProfile(pathsConfig);
            var fingerprint = ConfigFingerprint.Generate(pathsConfig, safetyConfig);

            output.ActiveProfile = pathsConfig.ActiveProfile;
            output.ConfigFingerprint = fingerprint;

            var status = new StatusCommand(_configDir).Execute();
            var doctor = new DoctorCommand(_configDir).Execute();
            var validate = new ValidateCommand(_configDir, _agentRoot).Execute();

            if (!doctor.Ok)
            {
                return CopyFailure(
                    output,
                    doctor,
                    "Agent health is degraded because doctor checks failed.");
            }

            var projectInspection = AgentCacheInspector.InspectProjectIndex(
                _agentRoot,
                pathsConfig.ActiveProfile,
                fingerprint,
                profile.RagnaforgeMainProjectPath);
            var entitiesInspection = AgentCacheInspector.InspectEntityIndex(
                _agentRoot,
                pathsConfig.ActiveProfile,
                fingerprint);

            var validateData = ToElement(validate.Data);

            output.Summary = "Operational health summary is ready.";
            output.SafeForAutomation = status.Ok && doctor.Ok;
            if (validateData.TryGetProperty("safeForApply", out var safeForApplyProp) && !safeForApplyProp.GetBoolean())
            {
                output.NextRequiredAction = "review_validation_issues_before_apply";
            }

            output.Data = new
            {
                mode = "health",
                agent = new
                {
                    version = AgentVersion.Current,
                    activeProfile = pathsConfig.ActiveProfile,
                    configFingerprint = fingerprint
                },
                project = new
                {
                    root = profile.RagnaforgeMainProjectPath,
                    filesIndexed = projectInspection.Document?.Stats.FilesIndexed ?? 0,
                    cacheTrusted = projectInspection.Details.CacheTrusted,
                    cacheStaleReason = projectInspection.Details.CacheStaleReason,
                    cacheFingerprint = projectInspection.Details.CacheFingerprint,
                    activeFingerprint = projectInspection.Details.ActiveFingerprint,
                    cacheProfile = projectInspection.Details.CacheProfile,
                    activeProfile = projectInspection.Details.ActiveProfile,
                    recommendedAction = projectInspection.Details.RecommendedAction
                },
                entities = new
                {
                    items = entitiesInspection.Document?.Stats.ItemsFound ?? 0,
                    monsters = entitiesInspection.Document?.Stats.MonstersFound ?? 0,
                    npcs = entitiesInspection.Document?.Stats.NpcsFound ?? 0,
                    maps = entitiesInspection.Document?.Stats.MapsFound ?? 0,
                    trustedCounts = entitiesInspection.Details.CacheTrusted,
                    cacheStaleReason = entitiesInspection.Details.CacheStaleReason,
                    cacheFingerprint = entitiesInspection.Details.CacheFingerprint,
                    activeFingerprint = entitiesInspection.Details.ActiveFingerprint,
                    cacheProfile = entitiesInspection.Details.CacheProfile,
                    activeProfile = entitiesInspection.Details.ActiveProfile,
                    recommendedAction = entitiesInspection.Details.RecommendedAction
                },
                validation = new
                {
                    issues = GetInt(validateData, "totalIssues"),
                    errors = GetInt(validateData, "errors"),
                    warnings = GetInt(validateData, "warnings"),
                    safeForReadOnlyWork = GetBool(validateData, "safeForReadOnlyWork"),
                    safeForDryRun = GetBool(validateData, "safeForDryRun"),
                    safeForApply = GetBool(validateData, "safeForApply")
                },
                safety = new
                {
                    applyBlocked = true,
                    rollbackRealBlocked = true,
                    grfReadOnly = true,
                    lubEditingBlocked = safetyConfig.BlockLubEditing
                },
                recommendedAction = output.NextRequiredAction
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("health", ex.Message);
        }

        return output;
    }

    private static JsonOutput CopyFailure(JsonOutput target, JsonOutput source, string summary)
    {
        target.Ok = false;
        target.Summary = summary;
        target.Errors.AddRange(source.Errors);
        target.Warnings.AddRange(source.Warnings);
        target.SafeForAutomation = false;
        target.NextRequiredAction = source.NextRequiredAction;
        target.Data = source.Data;
        return target;
    }

    private static JsonElement ToElement(object? data) =>
        data is null ? JsonDocument.Parse("{}").RootElement : JsonSerializer.SerializeToElement(data);

    private static int GetInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               property.GetBoolean();
    }
}
