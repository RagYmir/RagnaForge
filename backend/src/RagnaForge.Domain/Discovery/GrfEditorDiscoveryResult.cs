namespace RagnaForge.Domain.Discovery;

public sealed record GrfEditorExecutable(
    string Name,
    string FullPath,
    bool Exists,
    long Length,
    string? ProductName,
    string? ProductVersion,
    string? FileVersion);

public sealed record GrfEditorDiscoveryResult(
    string RootPath,
    bool Exists,
    IReadOnlyList<GrfEditorExecutable> Executables,
    IReadOnlyList<string> ConfigFiles,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Warnings);

