namespace RagnaForge.Application.Assets.RagnarokSprite;

public record SprMetadata(
    string SpriteVersion,
    int FrameCount,
    int IndexedFrameCount,
    int RgbaFrameCount,
    string RenderMode);

public record ActMetadata(
    int ActionCount,
    int SelectedAction,
    int SelectedFrame,
    int LayerCount,
    IReadOnlyList<int> ReferencedSpriteFrames);
