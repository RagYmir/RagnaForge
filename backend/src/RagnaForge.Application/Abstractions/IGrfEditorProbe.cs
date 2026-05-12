using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IGrfEditorProbe
{
    GrfEditorDiscoveryResult Probe(string grfEditorPath);
}

