namespace RagnaForge.Domain.Configuration;

public sealed record EpisodeProfile(
    string Name,
    EpisodeMode Mode,
    string? ClientDate,
    string Notes)
{
    public static EpisodeProfile Unknown { get; } = new(
        "unknown",
        EpisodeMode.Unknown,
        null,
        "No episode profile has been configured yet.");
}

