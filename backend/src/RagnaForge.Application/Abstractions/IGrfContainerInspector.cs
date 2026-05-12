using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IGrfContainerInspector
{
    GrfContainerInspectionResult Inspect(string grfEditorPath, string containerPath, int maxEntries);
}
