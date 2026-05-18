namespace RagnaForge.Api;

public sealed record ApiCapability(
    string Category,
    bool ReadOnly,
    bool DryRun,
    bool DiffPreview,
    bool Apply,
    bool Rollback,
    string Notes);

public static class ApiSafetyPolicy
{
    public static IReadOnlyList<string> DisabledWriteOperations { get; } =
    [
        "item.apply",
        "item.rollback",
        "equipment.apply",
        "equipment.rollback",
        "npc.apply",
        "npc.rollback",
        "monster.apply",
        "monster.rollback",
        "map.apply",
        "map.rollback"
    ];

    public static IReadOnlyList<ApiCapability> Capabilities { get; } =
    [
        new("config", true, false, false, false, false, "Configuration validation is read-only."),
        new("discovery", true, false, false, false, false, "Repository discovery reads rAthena, Patch, GRFs and GRF Editor."),
        new("grf", true, true, false, false, false, "GRF index/inspect may write local cache/index JSON inside the workspace only."),
        new("agent", true, false, false, false, false, "Agent health/diagnostics reads cached data and runs allowlisted read-only commands."),
        new("item", true, true, true, false, false, "Item apply/rollback is intentionally disabled in the first API cut."),
        new("equipment", true, true, true, false, false, "Equipment apply/rollback is intentionally disabled in the first API cut."),
        new("npc", true, true, true, false, false, "NPC apply/rollback is intentionally disabled in the first API cut."),
        new("monster", true, true, true, false, false, "Monster apply/rollback is intentionally disabled in the first API cut."),
        new("map", true, true, true, false, false, "Map apply remains blocked until real map dependency ambiguity is handled.")
    ];

    public static bool IsWriteOperationEnabled(string operation) => false;
}
