using System.Reflection;
using System.Runtime.Loader;
using RagnaForge.Application.Abstractions;
using RagnaForge.Domain.Configuration;

namespace RagnaForge.Infrastructure.GrfEditorIntegration;

public sealed class GrfAssemblySpriteRenderer : ISpriteRenderer
{
    public SpriteRenderResult Render(
        RepositoryPaths paths,
        byte[] assetBytes,
        string extension,
        int? frameIndex = null,
        int? actionIndex = null,
        byte[]? companionBytes = null)
    {
        var grfEditorPath = paths.GrfEditorPath;
        var assemblyPath = Path.Combine(grfEditorPath, "GRF.Core.dll");

        if (!File.Exists(assemblyPath))
        {
            return new SpriteRenderResult(null, null, null, "Unsupported", Errors: ["GRF.Core.dll not found in GrfEditorPath."]);
        }

        try
        {
            var context = new AssemblyLoadContext("GrfSpriteContext", isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
                return extension.ToLowerInvariant() switch
                {
                    ".spr" => RenderSprite(assembly, assetBytes, frameIndex),
                    ".act" => RenderAction(assembly, assetBytes, actionIndex, frameIndex, companionBytes),
                    _ => new SpriteRenderResult(null, null, null, "Unsupported", Errors: [$"Unsupported extension: {extension}"])
                };
            }
            finally
            {
                context.Unload();
            }
        }
        catch (Exception ex)
        {
            // Do not leak absolute paths or sensitive stack info
            return new SpriteRenderResult(null, null, null, "Error", Errors: [$"Failed to render {extension}: {ex.Message}"]);
        }
    }

    private SpriteRenderResult RenderSprite(Assembly assembly, byte[] bytes, int? frameIndex)
    {
        var sprType = assembly.GetType("GRF.FileFormats.SprFormat.Spr");
        if (sprType == null) return new SpriteRenderResult(null, null, null, "Unsupported", Errors: ["Spr type not found in assembly."]);

        using var ms = new MemoryStream(bytes);
        var sprInstance = Activator.CreateInstance(sprType, ms);

        var framesProp = sprType.GetProperty("Frames");
        var frames = framesProp?.GetValue(sprInstance) as System.Collections.IList;
        var frameCount = frames?.Count ?? 0;

        var selected = frameIndex ?? 0;
        if (selected < 0 || selected >= frameCount) selected = 0;

        byte[]? pngData = null;
        int? width = null;
        int? height = null;

        if (frameCount > 0)
        {
            var frame = frames?[selected];
            if (frame != null)
            {
                // Best-effort visual: try to get PngData property if it exists
                var pngDataProp = frame.GetType().GetProperty("PngData");
                pngData = pngDataProp?.GetValue(frame) as byte[];

                var widthProp = frame.GetType().GetProperty("Width");
                var heightProp = frame.GetType().GetProperty("Height");
                width = (int?)(widthProp?.GetValue(frame));
                height = (int?)(heightProp?.GetValue(frame));

                // Limit dimensions to prevent giant textures
                if (width > 2048 || height > 2048)
                {
                    pngData = null; // Block giant preview
                }
            }
        }

        return new SpriteRenderResult(
            pngData,
            width,
            height,
            pngData != null ? "SpriteFrame" : "SpriteMetadata",
            FrameCount: frameCount,
            SelectedFrame: selected);
    }

    private SpriteRenderResult RenderAction(Assembly assembly, byte[] bytes, int? actionIndex, int? frameIndex, byte[]? companionBytes)
    {
        var actType = assembly.GetType("GRF.FileFormats.ActFormat.Act");
        if (actType == null) return new SpriteRenderResult(null, null, null, "Unsupported", Errors: ["Act type not found in assembly."]);

        using var ms = new MemoryStream(bytes);
        var actInstance = Activator.CreateInstance(actType, ms);

        var actionsProp = actType.GetProperty("Actions");
        var actions = actionsProp?.GetValue(actInstance) as System.Collections.IList;
        var actionCount = actions?.Count ?? 0;

        var selectedAction = actionIndex ?? 0;
        if (selectedAction < 0 || selectedAction >= actionCount) selectedAction = 0;

        var framesProp = actType.GetProperty("Frames"); // Some versions might have global frame count or per action
        // ACT v1 implementation in RagnaForge is Metadata-Only for now
        
        return new SpriteRenderResult(
            null, 
            null, 
            null, 
            "ActMetadata",
            ActionCount: actionCount,
            SelectedAction: selectedAction,
            Warnings: ["ACT visual composition is metadata-only in v1."]);
    }
}
