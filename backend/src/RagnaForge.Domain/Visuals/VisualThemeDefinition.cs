namespace RagnaForge.Domain.Visuals;

public sealed record VisualThemeDefinition(
    string Key,
    string DisplayName,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> EquipLocations,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ResourceNamePatterns,
    IReadOnlyList<string> Notes);
