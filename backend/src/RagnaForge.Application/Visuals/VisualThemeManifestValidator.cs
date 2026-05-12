using System.Text.RegularExpressions;
using RagnaForge.Domain.Configuration;
using RagnaForge.Domain.Visuals;

namespace RagnaForge.Application.Visuals;

public sealed partial class VisualThemeManifestValidator
{
    private static readonly HashSet<string> KnownCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "headgear",
        "accessory",
        "robe",
        "shield",
        "weapon"
    };

    private static readonly HashSet<string> KnownEquipLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Head_Top",
        "Head_Mid",
        "Head_Low",
        "Garment",
        "Left_Hand",
        "Right_Hand",
        "Left_Accessory",
        "Right_Accessory",
        "Costume_Head_Top",
        "Costume_Head_Mid",
        "Costume_Head_Low",
        "Costume_Garment"
    };

    public ManifestValidationResult Validate(VisualThemeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<ManifestValidationIssue>();
        if (manifest.SchemaVersion != VisualThemeManifest.CurrentSchemaVersion)
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Error,
                "visual_theme.schema_version.unsupported",
                $"Unsupported visual theme schema '{manifest.SchemaVersion}'. Expected '{VisualThemeManifest.CurrentSchemaVersion}'."));
        }

        if (!string.Equals(manifest.Scope, VisualThemeManifest.CurrentScope, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Error,
                "visual_theme.scope.unsupported",
                $"Unsupported visual theme scope '{manifest.Scope}'. Expected '{VisualThemeManifest.CurrentScope}'."));
        }

        if (manifest.Themes.Count == 0)
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Warning,
                "visual_theme.empty",
                "No visual themes are configured."));
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var theme in manifest.Themes)
        {
            ValidateTheme(theme, issues, keys);
        }

        return issues.Count == 0 ? ManifestValidationResult.Valid : new ManifestValidationResult(issues);
    }

    private static void ValidateTheme(
        VisualThemeDefinition theme,
        List<ManifestValidationIssue> issues,
        HashSet<string> keys)
    {
        if (string.IsNullOrWhiteSpace(theme.Key) || !ThemeKeyRegex().IsMatch(theme.Key))
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Error,
                "visual_theme.key.invalid",
                $"Visual theme key is invalid: '{theme.Key}'."));
        }
        else if (!keys.Add(theme.Key))
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Error,
                "visual_theme.key.duplicate",
                $"Visual theme key is duplicated: '{theme.Key}'."));
        }

        if (string.IsNullOrWhiteSpace(theme.DisplayName))
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Error,
                "visual_theme.display_name.empty",
                $"Visual theme '{theme.Key}' has no display name."));
        }

        if (theme.Categories.Count == 0)
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Warning,
                "visual_theme.categories.empty",
                $"Visual theme '{theme.Key}' has no categories."));
        }

        foreach (var category in theme.Categories)
        {
            if (!KnownCategories.Contains(category))
            {
                issues.Add(new ManifestValidationIssue(
                    ManifestValidationSeverity.Warning,
                    "visual_theme.category.unknown",
                    $"Visual theme '{theme.Key}' uses unknown category '{category}'."));
            }
        }

        foreach (var equipLocation in theme.EquipLocations)
        {
            if (!KnownEquipLocations.Contains(equipLocation))
            {
                issues.Add(new ManifestValidationIssue(
                    ManifestValidationSeverity.Warning,
                    "visual_theme.equip_location.unknown",
                    $"Visual theme '{theme.Key}' uses unknown equip location '{equipLocation}'."));
            }
        }

        if (theme.Tags.Count == 0 && theme.ResourceNamePatterns.Count == 0)
        {
            issues.Add(new ManifestValidationIssue(
                ManifestValidationSeverity.Warning,
                "visual_theme.matchers.empty",
                $"Visual theme '{theme.Key}' has no tags or resource name patterns."));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeKeyRegex();
}
