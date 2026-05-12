using RagnaForge.Domain.Configuration;

namespace RagnaForge.Domain.Discovery;

public sealed record RepositoryDiscoveryReport(
    DateTimeOffset GeneratedAtUtc,
    RepositoryPaths Paths,
    EpisodeProfile EpisodeProfile,
    RathenaDiscoveryResult Rathena,
    PatchDiscoveryResult Patch,
    GrfRepositoryDiscoveryResult GrfRepository,
    GrfEditorDiscoveryResult GrfEditor);

