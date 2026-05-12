using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Configuration;

public sealed class ConfigurationManifestValidator
{
    public ManifestValidationResult Validate(ConfigurationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<ManifestValidationIssue>();

        if (manifest.SchemaVersion != ConfigurationManifest.CurrentSchemaVersion)
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Error,
                "schema_version.unsupported",
                $"Unsupported manifest schema '{manifest.SchemaVersion}'. Expected '{ConfigurationManifest.CurrentSchemaVersion}'."));
        }

        foreach (var (name, path) in manifest.Paths.Enumerate())
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                issues.Add(new ManifestValidationIssue(
                    ManifestValidationSeverity.Error,
                    "path.empty",
                    $"{name} path is empty."));
                continue;
            }

            if (!Directory.Exists(path))
            {
                issues.Add(new ManifestValidationIssue(
                    ManifestValidationSeverity.Error,
                    "path.missing",
                    $"{name} path does not exist: {path}"));
            }
        }

        if (manifest.IsProgressive && manifest.EpisodeProfile.Mode == EpisodeMode.Unknown)
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Warning,
                "episode.mode_unknown",
                "Progressive server manifest has no current episode mode configured."));
        }

        if (manifest.ClientDateStatus == "unknown")
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Warning,
                "client_date.unknown",
                "Client date is unknown; client-side dependency resolution must detect or request it later."));
        }

        return issues.Count == 0 ? ManifestValidationResult.Valid : new ManifestValidationResult(issues);
    }
}
