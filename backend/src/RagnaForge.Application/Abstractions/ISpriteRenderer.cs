using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Abstractions;

public record SpriteRenderResult(
    byte[]? ImageBytes,
    int? Width,
    int? Height,
    string? PreviewKind,
    int? FrameCount = null,
    int? ActionCount = null,
    int? SelectedFrame = null,
    int? SelectedAction = null,
    string? FormatVersion = null,
    string? RenderMode = null,
    int? LayerCount = null,
    IReadOnlyList<int>? ReferencedSpriteFrames = null,
    IReadOnlyList<string>? Warnings = null,
    IReadOnlyList<string>? Errors = null);

public interface ISpriteRenderer
{
    SpriteRenderResult Render(
        RepositoryPaths paths,
        byte[] assetBytes,
        string extension,
        int? frameIndex = null,
        int? actionIndex = null,
        byte[]? companionBytes = null);
}
