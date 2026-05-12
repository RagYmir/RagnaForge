using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IPatchScanner
{
    PatchDiscoveryResult Scan(string patchPath);
}

