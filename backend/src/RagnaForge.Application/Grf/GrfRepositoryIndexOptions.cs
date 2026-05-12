namespace RagnaForge.Application.Grf;

public sealed record GrfRepositoryIndexOptions(
    string RootPath,
    string CachePath,
    int MaxContainers = 200,
    bool ForceRefresh = false,
    bool SaveCache = true);
