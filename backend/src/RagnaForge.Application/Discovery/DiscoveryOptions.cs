using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Discovery;

public sealed record DiscoveryOptions(
    RepositoryPaths Paths,
    EpisodeProfile EpisodeProfile,
    int MaxGrfContainers = 200);

