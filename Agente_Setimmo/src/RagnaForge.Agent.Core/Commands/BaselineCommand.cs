using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Core.Scanning;

namespace RagnaForge.Agent.Core.Commands;

public sealed class BaselineCommand
{
    private readonly string _configDir;
    private readonly string _agentRoot;

    public BaselineCommand(string configDir, string agentRoot)
    {
        _configDir = configDir;
        _agentRoot = agentRoot;
    }

    public JsonOutput Execute()
    {
        var output = JsonOutput.Success("baseline");

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

            if (!doctor.Ok)
            {
                return Failure(output, status, doctor, null, null, null, "Doctor checks failed during baseline.");
            }

            var scan = new ScanCommand(_configDir, _agentRoot).Execute();
            if (!scan.Ok)
            {
                return Failure(output, status, doctor, scan, null, null, "Project scan failed during baseline.");
            }

            var index = new IndexCommand(_configDir, _agentRoot, "entities").Execute();
            if (!index.Ok)
            {
                return Failure(output, status, doctor, scan, index, null, "Entity indexing failed during baseline.");
            }

            var validate = new ValidateCommand(_configDir, _agentRoot).Execute();
            if (!validate.Ok)
            {
                return Failure(output, status, doctor, scan, index, validate, "Validation failed during baseline.");
            }

            var scanCache = AgentCacheInspector.InspectProjectIndex(
                _agentRoot,
                pathsConfig.ActiveProfile,
                fingerprint,
                profile.RagnaforgeMainProjectPath);
            var entityCache = AgentCacheInspector.InspectEntityIndex(
                _agentRoot,
                pathsConfig.ActiveProfile,
                fingerprint);

            var scanData = ToElement(scan.Data);
            var indexData = ToElement(index.Data);
            var validateData = ToElement(validate.Data);

            var safeForReadOnlyWork = GetBool(validateData, "safeForReadOnlyWork");
            var safeForDryRun = GetBool(validateData, "safeForDryRun");
            var safeForApply = GetBool(validateData, "safeForApply");
            output.SafeForAutomation = status.Ok && doctor.Ok && safeForReadOnlyWork;
            output.NextRequiredAction = safeForApply ? "none" : "review_validation_issues_before_apply";
            output.Summary = "Operational baseline completed.";

            output.Data = new
            {
                ok = true,
                mode = "baseline",
                operationId = output.OperationId,
                activeProfile = pathsConfig.ActiveProfile,
                configFingerprint = fingerprint,
                summary = output.Summary,
                status = new
                {
                    ok = status.Ok,
                    agentVersion = AgentVersion.Current,
                    projectRoot = profile.RagnaforgeMainProjectPath,
                    dbMode = profile.DbMode
                },
                doctor = new
                {
                    ok = doctor.Ok,
                    checks = GetArrayCount(ToElement(doctor.Data), "checks"),
                    warnings = doctor.Warnings.Count,
                    errors = doctor.Errors.Count
                },
                scan = new
                {
                    ok = scan.Ok,
                    filesIndexed = GetInt(scanData, "filesIndexed"),
                    cacheTrusted = scanCache.Details.CacheTrusted
                },
                index = new
                {
                    ok = index.Ok,
                    items = GetInt(indexData, "itemsFound"),
                    monsters = GetInt(indexData, "monstersFound"),
                    npcs = GetInt(indexData, "npcsFound"),
                    maps = GetInt(indexData, "mapsFound"),
                    cacheTrusted = entityCache.Details.CacheTrusted
                },
                validate = new
                {
                    ok = validate.Ok,
                    issues = GetInt(validateData, "totalIssues"),
                    errors = GetInt(validateData, "errors"),
                    warnings = GetInt(validateData, "warnings"),
                    safeForReadOnlyWork,
                    safeForDryRun,
                    safeForApply
                },
                decision = new
                {
                    safeForReadOnlyWork,
                    safeForDryRun,
                    safeForApply,
                    recommendedAction = output.NextRequiredAction
                }
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error("baseline", ex.Message);
        }

        return output;
    }

    private static JsonOutput Failure(
        JsonOutput output,
        JsonOutput status,
        JsonOutput doctor,
        JsonOutput? scan,
        JsonOutput? index,
        JsonOutput? validate,
        string summary)
    {
        output.Ok = false;
        output.Summary = summary;
        output.SafeForAutomation = false;
        output.NextRequiredAction = "fix_errors";

        foreach (var source in new[] { status, doctor, scan, index, validate }.Where(s => s is not null))
        {
            output.Errors.AddRange(source!.Errors);
            output.Warnings.AddRange(source.Warnings);
        }

        output.Data = new
        {
            status = status.Data,
            doctor = doctor.Data,
            scan = scan?.Data,
            index = index?.Data,
            validate = validate?.Data
        };
        return output;
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

    private static int GetArrayCount(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
    }
}
