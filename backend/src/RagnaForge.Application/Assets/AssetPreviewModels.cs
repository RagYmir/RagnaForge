using System.Text.Json.Serialization;

namespace RagnaForge.Application.Assets;

public sealed record AssetPreviewRequest(
    string Source,
    string Container,
    string EntryPath,
    string ExpectedExtension,
    string? ConfigPath = null,
    long MaxBytes = 1048576,
    int? FrameIndex = null,
    int? ActionIndex = null,
    string? CompanionEntryPath = null);

public sealed record AssetPreviewResponse(
    string AssetName,
    string EntryPath,
    string Extension,
    string? ContentType,
    string PreviewKind,
    string? DataUrl,
    int? Width,
    int? Height,
    string Source,
    string Provenance,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    AssetPreviewMetadata? Metadata = null);

public sealed record AssetPreviewMetadata(
    int? FrameCount = null,
    int? ActionCount = null,
    int? SelectedFrame = null,
    int? SelectedAction = null,
    string? FormatVersion = null,
    string? RenderMode = null,
    int? LayerCount = null,
    IReadOnlyList<int>? ReferencedSpriteFrames = null,
    IReadOnlyDictionary<string, string>? Extra = null);
