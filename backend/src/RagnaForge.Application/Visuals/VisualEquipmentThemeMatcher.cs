using RagnaForge.Domain.Items;
using RagnaForge.Domain.Visuals;

namespace RagnaForge.Application.Visuals;

public sealed class VisualEquipmentThemeMatcher
{
    private sealed record ScoredTheme(VisualThemeDefinition Theme, VisualThemeSuggestion Suggestion);

    public VisualThemeEvaluation Evaluate(
        VisualThemeManifest manifest,
        EquipmentDefinitionInput input,
        string manifestPath,
        string? requestedKey,
        int maxSuggestions = 3)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var scoredThemes = manifest.Themes
            .Select(theme => new ScoredTheme(theme, ScoreTheme(theme, input)))
            .Where(item => item.Suggestion.Score > 0)
            .OrderByDescending(item => item.Suggestion.Score)
            .ThenByDescending(item => item.Suggestion.MatchedPatterns.Count)
            .ThenBy(item => item.Suggestion.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suggestions = scoredThemes
            .Select(item => item.Suggestion)
            .Take(Math.Max(1, maxSuggestions))
            .ToArray();

        var issues = new List<string>();
        VisualThemeSuggestion? selectedTheme = null;
        VisualThemeDefinition? selectedThemeDefinition = null;

        if (!string.IsNullOrWhiteSpace(requestedKey))
        {
            var theme = manifest.Themes.FirstOrDefault(item =>
                item.Key.Equals(requestedKey, StringComparison.OrdinalIgnoreCase));

            if (theme is null)
            {
                issues.Add($"Visual theme '{requestedKey}' was not found in the local manifest.");
            }
            else
            {
                selectedThemeDefinition = theme;
                selectedTheme = ScoreTheme(theme, input);

                var normalizedCategory = NormalizeVisualCategory(input.VisualCategory);
                if (normalizedCategory is not null
                    && !selectedTheme.MatchedCategories.Contains(normalizedCategory, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add($"Visual theme '{theme.DisplayName}' does not cover visual category '{normalizedCategory}'.");
                }

                if (input.EquipLocations.Count > 0 && selectedTheme.MatchedEquipLocations.Count == 0)
                {
                    issues.Add($"Visual theme '{theme.DisplayName}' does not cover any of the current equip locations.");
                }

                if (selectedTheme.MatchedPatterns.Count == 0)
                {
                    issues.Add($"Visual theme '{theme.DisplayName}' did not match the current resource/client names by pattern.");
                }
            }
        }

        var lookupTokens = BuildLookupTokens(selectedThemeDefinition, selectedTheme, suggestions, scoredThemes);

        return new VisualThemeEvaluation(
            manifestPath,
            manifest.Scope,
            requestedKey,
            selectedTheme,
            suggestions,
            lookupTokens,
            issues);
    }

    private static VisualThemeSuggestion ScoreTheme(VisualThemeDefinition theme, EquipmentDefinitionInput input)
    {
        var normalizedCategory = NormalizeVisualCategory(input.VisualCategory);
        var matchedCategories = normalizedCategory is null
            ? Array.Empty<string>()
            : theme.Categories
                .Where(category => category.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var matchedLocations = theme.EquipLocations
            .Intersect(input.EquipLocations, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidateNames = BuildCandidateNames(input);
        var matchedPatterns = theme.ResourceNamePatterns
            .Where(pattern => candidateNames.Any(candidate =>
                candidate.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var score = 0;
        if (matchedCategories.Length > 0)
        {
            score += 3;
        }

        if (matchedLocations.Length > 0)
        {
            score += 2;
        }

        score += matchedPatterns.Length;

        return new VisualThemeSuggestion(
            theme.Key,
            theme.DisplayName,
            score,
            matchedCategories,
            matchedLocations,
            matchedPatterns);
    }

    private static string[] BuildCandidateNames(EquipmentDefinitionInput input) =>
        new[]
        {
            input.Item.AegisName,
            input.Item.DisplayName,
            input.Item.ResourceName,
            input.ClientSymbolName ?? string.Empty,
            input.ClientSpriteName ?? string.Empty
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string[] BuildLookupTokens(
        VisualThemeDefinition? selectedTheme,
        VisualThemeSuggestion? selectedSuggestion,
        IReadOnlyList<VisualThemeSuggestion> suggestions,
        IReadOnlyList<ScoredTheme> scoredThemes)
    {
        IEnumerable<string> tokens = selectedTheme is not null
            ? (selectedSuggestion is { MatchedPatterns.Count: > 0 }
                ? selectedSuggestion.MatchedPatterns
                : selectedTheme.ResourceNamePatterns)
            : scoredThemes
                .Where(item => suggestions
                    .Take(2)
                    .Any(suggestion => suggestion.Key.Equals(item.Theme.Key, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(item => item.Suggestion.MatchedPatterns.Count > 0
                    ? item.Suggestion.MatchedPatterns
                    : item.Theme.ResourceNamePatterns);

        return tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(NormalizeLookupToken)
            .Where(token => token.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(token => token.Length)
            .ThenBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeLookupToken(string value) =>
        new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static string? NormalizeVisualCategory(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "headgear" or "costume-head" or "costume_head" => "headgear",
            "accessory" => "accessory",
            "robe" or "garment" => "robe",
            "weapon" => "weapon",
            "shield" => "shield",
            _ => value?.Trim().ToLowerInvariant()
        };
}
