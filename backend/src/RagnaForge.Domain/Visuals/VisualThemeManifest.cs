namespace RagnaForge.Domain.Visuals;

public sealed record VisualThemeManifest(
    string SchemaVersion,
    string Scope,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<VisualThemeDefinition> Themes,
    IReadOnlyList<string> Notes)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentScope = "equipment-visuals";

    public static VisualThemeManifest Create(
        IEnumerable<VisualThemeDefinition> themes,
        DateTimeOffset? timestampUtc = null,
        IEnumerable<string>? notes = null)
    {
        var timestamp = timestampUtc ?? DateTimeOffset.UtcNow;
        return new VisualThemeManifest(
            CurrentSchemaVersion,
            CurrentScope,
            timestamp,
            timestamp,
            themes.ToArray(),
            notes?.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray() ?? []);
    }

    public static VisualThemeManifest CreateDefault(DateTimeOffset? timestampUtc = null) =>
        Create(
            [
                new VisualThemeDefinition(
                    "angelical",
                    "Angelical",
                    ["headgear", "accessory", "robe", "shield", "weapon"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment", "Left_Hand", "Right_Hand"],
                    ["asas", "luz", "sagrado", "divino"],
                    ["angel", "wing", "holy", "light", "archangel"],
                    ["Tema para equipamentos visuais claros, sagrados e de asas."]),
                new VisualThemeDefinition(
                    "sombrio",
                    "Sombrio",
                    ["headgear", "accessory", "robe", "shield", "weapon"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment", "Left_Hand", "Right_Hand"],
                    ["dark", "morto-vivo", "demonio", "gothic"],
                    ["dark", "shadow", "demon", "death", "lord_of_death", "skull"],
                    ["Tema para equipamentos visuais escuros, demoniacos e goticos."]),
                new VisualThemeDefinition(
                    "elemental",
                    "Elemental",
                    ["headgear", "accessory", "robe", "shield", "weapon"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment", "Left_Hand", "Right_Hand"],
                    ["fogo", "agua", "vento", "terra", "gelo"],
                    ["fire", "flame", "water", "wind", "earth", "ice", "storm"],
                    ["Tema para equipamentos visuais ligados a elementos e efeitos naturais."]),
                new VisualThemeDefinition(
                    "fofo",
                    "Fofo",
                    ["headgear", "accessory", "robe"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment"],
                    ["cute", "pet", "animal", "chibi"],
                    ["rabbit", "cat", "dog", "poring", "cute", "loli"],
                    ["Tema para equipamentos visuais leves, mascotes e costumes carismaticos."]),
                new VisualThemeDefinition(
                    "mecanico",
                    "Mecanico",
                    ["headgear", "accessory", "robe", "shield", "weapon"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment", "Left_Hand", "Right_Hand"],
                    ["metal", "robo", "laboratorio", "engrenagem"],
                    ["mech", "robot", "metal", "gear", "engine", "clock"],
                    ["Tema para equipamentos visuais tecnologicos, mecanicos ou industriais."]),
                new VisualThemeDefinition(
                    "sazonal",
                    "Sazonal",
                    ["headgear", "accessory", "robe", "shield", "weapon"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment", "Left_Hand", "Right_Hand"],
                    ["natal", "halloween", "evento", "festival"],
                    ["xmas", "christmas", "halloween", "event", "festival", "summer"],
                    ["Tema para equipamentos visuais de eventos temporarios e pacotes comemorativos."]),
                new VisualThemeDefinition(
                    "guerra",
                    "Guerra",
                    ["headgear", "accessory", "robe", "shield", "weapon"],
                    ["Costume_Head_Top", "Costume_Head_Mid", "Costume_Head_Low", "Costume_Garment", "Left_Hand", "Right_Hand"],
                    ["woe", "castelo", "militar", "batalha"],
                    ["war", "battle", "guard", "castle", "knight", "shield"],
                    ["Tema para equipamentos visuais de combate, castelos e Guerra do Emperium."])
            ],
            timestampUtc,
            [
                "Visual equipment themes are local metadata used to classify in-game visual equipment.",
                "They do not replace rAthena, Patch/client or GRF repositories as source of truth."
            ]);
}
