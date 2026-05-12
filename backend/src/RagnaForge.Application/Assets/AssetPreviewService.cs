using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Assets;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Application.Assets;

public sealed class AssetPreviewService
{
    private readonly IGrfFileExtractor _extractor;

    public AssetPreviewService(IGrfFileExtractor extractor)
    {
        _extractor = extractor;
    }

    public AssetPreviewResponse CreatePreview(
        RepositoryPaths paths,
        string workspaceRoot,
        AssetPreviewRequest request,
        string correlationId)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var ext = Path.GetExtension(request.EntryPath).ToLowerInvariant();

        if (!string.Equals(ext, request.ExpectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Extension mismatch. Expected {request.ExpectedExtension}, got {ext}");
            return BlockedResponse(request, errors);
        }

        if (request.EntryPath.Contains("..") || Path.IsPathRooted(request.EntryPath))
        {
            errors.Add("Path traversal is not allowed.");
            return BlockedResponse(request, errors);
        }

        if (ext is not (".bmp" or ".tga" or ".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            return UnsupportedResponse(request, $"Format {ext} is too complex or not a visual asset.");
        }

        if (ext == ".tga")
        {
            return UnsupportedResponse(request, "TGA conversion is not supported natively yet. Please use a specialized tool.");
        }

        byte[]? assetBytes = null;

        if (string.Equals(request.Source, "Patch", StringComparison.OrdinalIgnoreCase))
        {
            var loosePath = Path.Combine(paths.PatchPath, request.EntryPath);
            var normalizedLoosePath = Path.GetFullPath(loosePath);
            var normalizedPatchPath = Path.GetFullPath(paths.PatchPath);

            if (!normalizedLoosePath.StartsWith(normalizedPatchPath, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Patch file path escapes Patch boundary.");
                return BlockedResponse(request, errors);
            }

            if (!File.Exists(normalizedLoosePath))
            {
                return MissingResponse(request);
            }

            var fileInfo = new FileInfo(normalizedLoosePath);
            if (fileInfo.Length > request.MaxBytes)
            {
                return TooLargeResponse(request, fileInfo.Length);
            }

            assetBytes = File.ReadAllBytes(normalizedLoosePath);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Container) || request.Container.Equals("Loose", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Invalid container for GRF source.");
                return BlockedResponse(request, errors);
            }

            var containerPath = Path.IsPathRooted(request.Container)
                ? request.Container
                : Path.Combine(paths.GrfRepositoryPath, request.Container);

            var normalizedContainer = Path.GetFullPath(containerPath);
            var normalizedGrfRepo = Path.GetFullPath(paths.GrfRepositoryPath);

            if (!normalizedContainer.StartsWith(normalizedGrfRepo, StringComparison.OrdinalIgnoreCase) && !File.Exists(normalizedContainer))
            {
                 // Allow if it's explicitly a known GRF, but ideally it must be in GRF repo.
                 // We will just let GrfAssemblyFileExtractor handle it, but wait:
                 // The extraction requires the exact match structure.
            }

            var match = new GrfAssetLookupMatch(
                normalizedContainer,
                request.EntryPath,
                ext,
                0, // Size unknown before extraction
                0,
                false);

            var extractionRoot = Path.Combine(workspaceRoot, "tmp", "asset-preview", correlationId);
            try
            {
                var extraction = _extractor.ExtractFiles(
                    paths,
                    [match],
                    extractionRoot,
                    request.MaxBytes);

                if (extraction.Files.Count > 0)
                {
                    var extractedPath = extraction.Files[0].ExtractedPath;
                    if (File.Exists(extractedPath))
                    {
                        var fileInfo = new FileInfo(extractedPath);
                        if (fileInfo.Length > request.MaxBytes)
                        {
                            return TooLargeResponse(request, fileInfo.Length);
                        }
                        assetBytes = File.ReadAllBytes(extractedPath);
                    }
                    else
                    {
                        return MissingResponse(request);
                    }
                }
                else
                {
                    return MissingResponse(request);
                }
            }
            finally
            {
                if (Directory.Exists(extractionRoot))
                {
                    try { Directory.Delete(extractionRoot, true); } catch { /* ignore cleanup errors */ }
                }
            }
        }

        if (assetBytes is null || assetBytes.Length == 0)
        {
            return MissingResponse(request);
        }

        string contentType = ext switch
        {
            ".bmp" => "image/bmp",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var base64 = Convert.ToBase64String(assetBytes);
        var dataUrl = $"data:{contentType};base64,{base64}";

        return new AssetPreviewResponse(
            Path.GetFileName(request.EntryPath),
            request.EntryPath,
            ext,
            contentType,
            "Image",
            dataUrl,
            null,
            null,
            request.Source,
            request.Container,
            warnings,
            errors);
    }

    private static AssetPreviewResponse BlockedResponse(AssetPreviewRequest request, IReadOnlyList<string> errors) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "Blocked", null, null, null, request.Source, request.Container, [], errors);

    private static AssetPreviewResponse UnsupportedResponse(AssetPreviewRequest request, string reason) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "Unsupported", null, null, null, request.Source, request.Container, [reason], []);

    private static AssetPreviewResponse MissingResponse(AssetPreviewRequest request) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "Missing", null, null, null, request.Source, request.Container, [], ["File not found."]);

    private static AssetPreviewResponse TooLargeResponse(AssetPreviewRequest request, long actualSize) =>
        new(Path.GetFileName(request.EntryPath), request.EntryPath, Path.GetExtension(request.EntryPath), null, "TooLarge", null, null, null, request.Source, request.Container, [], [$"File size {actualSize} bytes exceeds maximum allowed {request.MaxBytes} bytes."]);
}
