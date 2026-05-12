namespace RagnaForge.Domain.Assets;

public sealed record GrfExtractedFile(
    string ContainerPath,
    string RelativePath,
    string ExtractedPath,
    long Length);

public sealed record GrfFileExtractionResult(
    bool Attempted,
    string ExtractionRoot,
    IReadOnlyList<GrfExtractedFile> Files,
    IReadOnlyList<string> Warnings);
