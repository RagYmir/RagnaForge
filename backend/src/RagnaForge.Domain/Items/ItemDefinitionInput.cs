namespace RagnaForge.Domain.Items;

public sealed record ItemDefinitionInput(
    int? Id,
    string AegisName,
    string DisplayName,
    string ResourceName,
    string Type,
    int Buy,
    int Sell,
    int Weight,
    int Slots,
    string? Script,
    IReadOnlyList<string> IdentifiedDescriptionLines,
    string? UnidentifiedDisplayName,
    string? UnidentifiedResourceName,
    IReadOnlyList<string> UnidentifiedDescriptionLines);
