using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Discovery;

public sealed class RepositoryDiscoveryService(
    IRathenaScanner rathenaScanner,
    IPatchScanner patchScanner,
    IGrfRepositoryScanner grfRepositoryScanner,
    IGrfEditorProbe grfEditorProbe)
{
    public RepositoryDiscoveryReport Run(DiscoveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RepositoryDiscoveryReport(
            DateTimeOffset.UtcNow,
            options.Paths,
            options.EpisodeProfile,
            rathenaScanner.Scan(options.Paths.RathenaPath),
            patchScanner.Scan(options.Paths.PatchPath),
            grfRepositoryScanner.Scan(options.Paths.GrfRepositoryPath, options.MaxGrfContainers),
            grfEditorProbe.Probe(options.Paths.GrfEditorPath));
    }
}

