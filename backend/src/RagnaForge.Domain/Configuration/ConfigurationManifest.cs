namespace RagnaForge.Domain.Configuration;

public sealed record ConfigurationManifest(
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    RepositoryPaths Paths,
    EpisodeProfile EpisodeProfile,
    bool IsProgressive,
    string ClientDateStatus,
    IReadOnlyList<string> Notes)
{
    public const string CurrentSchemaVersion = "1.0";

    public static ConfigurationManifest Create(
        RepositoryPaths paths,
        EpisodeProfile episodeProfile,
        bool isProgressive,
        DateTimeOffset? timestampUtc = null,
        IEnumerable<string>? notes = null)
    {
        var timestamp = timestampUtc ?? DateTimeOffset.UtcNow;

        return new ConfigurationManifest(
            CurrentSchemaVersion,
            timestamp,
            timestamp,
            paths,
            episodeProfile,
            isProgressive,
            string.IsNullOrWhiteSpace(episodeProfile.ClientDate) ? "unknown" : "configured",
            notes?.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray() ?? []);
    }
}
