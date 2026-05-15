using System.Text.Json.Serialization;

namespace RagnaForge.Application.Assets;

public sealed record AssetPreviewRequest(
    string Source,
    string Container,
    string EntryPath,
    string ExpectedExtension,
    string? ConfigPath = null,
    long MaxBytes = 1048576);

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
    IReadOnlyList<string> Errors);
