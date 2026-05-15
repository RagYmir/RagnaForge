using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Assets;

public sealed class AssetPreviewService
{
    private readonly IGrfFileExtractor _extractor;
    private readonly ISpriteRenderer? _spriteRenderer;

    public AssetPreviewService(IGrfFileExtractor extractor, ISpriteRenderer? spriteRenderer = null)
    {
        _extractor = extractor;
        _spriteRenderer = spriteRenderer;
    }

    public AssetPreviewResponse CreatePreview(
        RepositoryPaths paths,
        string workspaceRoot,
        AssetPreviewRequest request,
        string correlationId)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Normalize paths early
        var entryPath = PathValidationHelper.NormalizePath(request.EntryPath);
        var companionPath = PathValidationHelper.NormalizePath(request.CompanionEntryPath ?? string.Empty);
        var ext = Path.GetExtension(entryPath).ToLowerInvariant();

        // 1. Basic Path & Extension Validation
        if (!string.Equals(ext, request.ExpectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Extension mismatch. Expected {request.ExpectedExtension}, got {ext}");
            return BlockedResponse(request, errors);
        }

        if (!PathValidationHelper.IsSafeLogicalPath(entryPath))
        {
            errors.Add("Invalid entry path or path traversal detected.");
            return BlockedResponse(request, errors);
        }

        if (!string.IsNullOrWhiteSpace(companionPath))
        {
            if (!PathValidationHelper.IsSafeCompanionPath(entryPath, companionPath, ".spr"))
            {
                errors.Add("Invalid companion path or boundary escape detected.");
                return BlockedResponse(request, errors);
            }
        }

        // 2. Resource Limits
        const long GlobalMaxBytes = 10 * 1024 * 1024;
        var effectiveMaxBytes = Math.Min(request.MaxBytes, GlobalMaxBytes);

        if (ext is not (".bmp" or ".tga" or ".png" or ".jpg" or ".jpeg" or ".webp" or ".spr" or ".act"))
        {
            return UnsupportedResponse(request, $"Format {ext} is too complex or not a visual asset.");
        }

        if (ext == ".tga")
        {
            return UnsupportedResponse(request, "TGA conversion is not supported natively yet.");
        }

        byte[]? assetBytes = null;
        byte[]? companionBytes = null;

        // 3. Extraction/Reading Logic
        if (string.Equals(request.Source, "Patch", StringComparison.OrdinalIgnoreCase))
        {
            var patchRes = ReadPatchFile(paths.PatchPath, entryPath, effectiveMaxBytes);
            if (patchRes.Error != null)
            {
                if (patchRes.IsTooLarge) return TooLargeResponse(request, patchRes.Size, effectiveMaxBytes);
                errors.Add(patchRes.Error);
                return BlockedResponse(request, errors);
            }
            assetBytes = patchRes.Bytes;
            if (assetBytes == null) return MissingResponse(request);

            if (ext == ".act" && !string.IsNullOrWhiteSpace(companionPath))
            {
                var companionRes = ReadPatchFile(paths.PatchPath, companionPath, effectiveMaxBytes);
                companionBytes = companionRes.Bytes;
            }
        }
        else if (string.Equals(request.Source, "GRF", StringComparison.OrdinalIgnoreCase))
        {
            if (!ValidateContainer(paths, request.Container, errors)) return BlockedResponse(request, errors);

            var extractionRoot = Path.Combine(workspaceRoot, "tmp", "asset-preview", correlationId);
            try
            {
                var matches = new List<GrfAssetLookupMatch>
                {
                    new(Path.Combine(paths.GrfRepositoryPath, request.Container), entryPath, ext, 0, 0, false)
                };

                if (ext == ".act" && !string.IsNullOrWhiteSpace(companionPath))
                {
                    matches.Add(new(Path.Combine(paths.GrfRepositoryPath, request.Container), companionPath, ".spr", 0, 0, false));
                }

                var extraction = _extractor.ExtractFiles(paths, matches, extractionRoot, effectiveMaxBytes);
                
                var primary = extraction.Files.FirstOrDefault(f => f.RelativePath.Equals(entryPath, StringComparison.OrdinalIgnoreCase));
                if (primary != null && File.Exists(primary.ExtractedPath))
                {
                    var info = new FileInfo(primary.ExtractedPath);
                    if (info.Length > effectiveMaxBytes) return TooLargeResponse(request, info.Length, effectiveMaxBytes);
                    assetBytes = File.ReadAllBytes(primary.ExtractedPath);
                }

                if (ext == ".act" && !string.IsNullOrWhiteSpace(companionPath))
                {
                    var companion = extraction.Files.FirstOrDefault(f => f.RelativePath.Equals(companionPath, StringComparison.OrdinalIgnoreCase));
                    if (companion != null && File.Exists(companion.ExtractedPath))
                    {
                        companionBytes = File.ReadAllBytes(companion.ExtractedPath);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(extractionRoot))
                {
                    try { Directory.Delete(extractionRoot, true); } catch { }
                }
            }

            if (assetBytes == null) return MissingResponse(request);
        }
        else
        {
            errors.Add($"Unknown source: {request.Source}");
            return BlockedResponse(request, errors);
        }

        // 4. Rendering & Metadata Extraction
        if (ext is ".spr" or ".act")
        {
            if (_spriteRenderer == null)
            {
                return UnsupportedResponse(request, $"Sprite renderer is not configured for {ext}.");
            }

            var renderResult = _spriteRenderer.Render(paths, assetBytes, ext, request.FrameIndex, request.ActionIndex, companionBytes);
            if (renderResult.Errors?.Any() == true)
            {
                errors.AddRange(renderResult.Errors);
                return BlockedResponse(request, errors);
            }

            if (renderResult.Warnings?.Any() == true)
            {
                warnings.AddRange(renderResult.Warnings);
            }

            string? dataUrl = null;
            if (renderResult.ImageBytes != null)
            {
                dataUrl = $"data:image/png;base64,{Convert.ToBase64String(renderResult.ImageBytes)}";
            }

            return new AssetPreviewResponse(
                Path.GetFileName(entryPath),
                entryPath,
                ext,
                renderResult.ImageBytes != null ? "image/png" : null,
                renderResult.PreviewKind ?? "Unsupported",
                dataUrl,
                renderResult.Width,
                renderResult.Height,
                request.Source,
                request.Container,
                warnings,
                errors,
                new AssetPreviewMetadata(
                    renderResult.FrameCount,
                    renderResult.ActionCount,
                    renderResult.SelectedFrame ?? request.FrameIndex,
                    renderResult.SelectedAction ?? request.ActionIndex,
                    renderResult.FormatVersion,
                    renderResult.RenderMode,
                    renderResult.LayerCount,
                    renderResult.ReferencedSpriteFrames));
        }

        // 5. Bitmap Handlers
        string contentType = ext switch
        {
            ".bmp" => "image/bmp",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return new AssetPreviewResponse(
            Path.GetFileName(entryPath),
            entryPath,
            ext,
            contentType,
            "Image",
            $"data:{contentType};base64,{Convert.ToBase64String(assetBytes)}",
            null,
            null,
            request.Source,
            request.Container,
            warnings,
            errors);
    }

    private record PatchReadResult(byte[]? Bytes, string? Error = null, bool IsTooLarge = false, long Size = 0);

    private PatchReadResult ReadPatchFile(string patchPath, string entryPath, long maxBytes)
    {
        var loosePath = Path.Combine(patchPath, entryPath);
        
        if (!PathValidationHelper.IsWithinBoundary(patchPath, loosePath))
        {
            return new PatchReadResult(null, "File path escapes Patch boundary.");
        }

        if (!File.Exists(loosePath)) return new PatchReadResult(null);

        var fileInfo = new FileInfo(loosePath);
        if (fileInfo.Length > maxBytes)
        {
            return new PatchReadResult(null, $"File size {fileInfo.Length} exceeds limit {maxBytes}.", true, fileInfo.Length);
        }

        return new PatchReadResult(File.ReadAllBytes(loosePath));
    }

    private bool ValidateContainer(RepositoryPaths paths, string container, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(container) || container.Equals("Loose", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Invalid container for GRF source.");
            return false;
        }

        // Logic path check for container name
        if (!PathValidationHelper.IsSafeLogicalPath(container))
        {
            errors.Add("Invalid container path or traversal detected.");
            return false;
        }

        var containerPath = Path.Combine(paths.GrfRepositoryPath, container);
        if (!PathValidationHelper.IsWithinBoundary(paths.GrfRepositoryPath, containerPath))
        {
            errors.Add("GRF container escapes GRF repository boundary.");
            return false;
        }

        if (!File.Exists(containerPath))
        {
            errors.Add("GRF container not found.");
            return false;
        }

        return true;
    }

    private static AssetPreviewResponse BlockedResponse(AssetPreviewRequest request, IReadOnlyList<string> errors) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "Blocked", null, null, null, request.Source, request.Container, [], errors);

    private static AssetPreviewResponse UnsupportedResponse(AssetPreviewRequest request, string reason) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "Unsupported", null, null, null, request.Source, request.Container, [reason], []);

    private static AssetPreviewResponse MissingResponse(AssetPreviewRequest request) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "Missing", null, null, null, request.Source, request.Container, [], ["File not found."]);

    private static AssetPreviewResponse TooLargeResponse(AssetPreviewRequest request, long actualSize, long limit) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "TooLarge", null, null, null, request.Source, request.Container, [], [$"File size {actualSize} bytes exceeds maximum allowed {limit} bytes."]);
}
