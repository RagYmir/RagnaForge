using RagnaForge.Domain.Discovery;

namespace RagnaForge.Application.Abstractions;

public interface IRathenaScanner
{
    RathenaDiscoveryResult Scan(string rathenaPath);
}

