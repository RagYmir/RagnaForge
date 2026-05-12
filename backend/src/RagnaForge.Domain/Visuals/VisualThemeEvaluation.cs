namespace RagnaForge.Domain.Visuals;

public sealed record VisualThemeSuggestion(
    string Key,
    string DisplayName,
    int Score,
    IReadOnlyList<string> MatchedCategories,
    IReadOnlyList<string> MatchedEquipLocations,
    IReadOnlyList<string> MatchedPatterns);

public sealed record VisualThemeEvaluation(
    string ManifestPath,
    string Scope,
    string? RequestedKey,
    VisualThemeSuggestion? SelectedTheme,
    IReadOnlyList<VisualThemeSuggestion> SuggestedThemes,
    IReadOnlyList<string> LookupTokens,
    IReadOnlyList<string> Issues);
