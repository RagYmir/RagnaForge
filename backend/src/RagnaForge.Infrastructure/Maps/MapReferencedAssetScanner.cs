using System.Text.RegularExpressions;
using RagnaForge.Domain.Maps;

namespace RagnaForge.Infrastructure.Maps;

internal sealed partial class MapReferencedAssetScanner
{
    private static readonly HashSet<string> TextureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".png", ".tga", ".jpg", ".jpeg"
    };

    private static readonly HashSet<string> ModelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rsm", ".rsm2"
    };

    private static readonly HashSet<string> SoundExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".ogg"
    };

    private static readonly HashSet<string> EffectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".str"
    };

    private static readonly HashSet<string> SpriteExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".spr", ".act"
    };

    public MapDependencyScanResult Scan(string? rswPath, string? gndPath)
    {
        var warnings = new List<string>();
        var references = new Dictionary<string, MapReferencedAsset>(StringComparer.OrdinalIgnoreCase);
        var deepScanAvailable = false;

        if (File.Exists(rswPath))
        {
            deepScanAvailable = true;
            CollectReferences(rswPath!, ".rsw", references, warnings);
        }

        if (File.Exists(gndPath))
        {
            deepScanAvailable = true;
            CollectReferences(gndPath!, ".gnd", references, warnings);
        }

        if (!deepScanAvailable)
        {
            warnings.Add("Deep map dependency scan requires accessible loose .rsw or .gnd files; this run only validated the core trio.");
        }

        return new MapDependencyScanResult(
            deepScanAvailable,
            "LoosePatch",
            references.Values
                .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ReferencePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            warnings);
    }

    private static void CollectReferences(
        string path,
        string sourceFileType,
        Dictionary<string, MapReferencedAsset> references,
        List<string> warnings)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var buffer = new List<byte>(128);
            foreach (var value in bytes)
            {
                if (IsAsciiTextByte(value))
                {
                    buffer.Add(value);
                    continue;
                }

                FlushBuffer(buffer, sourceFileType, references);
            }

            FlushBuffer(buffer, sourceFileType, references);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not inspect map dependency strings from {path}: {ex.Message}");
        }
    }

    private static void FlushBuffer(
        List<byte> buffer,
        string sourceFileType,
        Dictionary<string, MapReferencedAsset> references)
    {
        if (buffer.Count < 4)
        {
            buffer.Clear();
            return;
        }

        var text = System.Text.Encoding.ASCII.GetString(buffer.ToArray());
        foreach (Match match in ResourceReferenceRegex().Matches(text))
        {
            var referencePath = NormalizeReferencePath(match.Value);
            var extension = Path.GetExtension(referencePath);
            var category = ClassifyCategory(extension);
            if (category is null)
            {
                continue;
            }

            if (!references.ContainsKey(referencePath))
            {
                references.Add(referencePath, new MapReferencedAsset(
                    category,
                    referencePath,
                    sourceFileType,
                    false,
                    null,
                    null,
                    false));
            }
        }

        buffer.Clear();
    }

    private static bool IsAsciiTextByte(byte value) =>
        value is 9 or 10 or 13 || (value >= 32 && value <= 126);

    private static string NormalizeReferencePath(string value) =>
        value.Trim()
            .Trim('"')
            .Trim('\'')
            .Replace('/', '\\')
            .TrimStart('\\');

    private static string? ClassifyCategory(string extension)
    {
        if (TextureExtensions.Contains(extension))
        {
            return "Texture";
        }

        if (ModelExtensions.Contains(extension))
        {
            return "Model";
        }

        if (SoundExtensions.Contains(extension))
        {
            return "Sound";
        }

        if (EffectExtensions.Contains(extension))
        {
            return "Effect";
        }

        if (SpriteExtensions.Contains(extension))
        {
            return "Sprite";
        }

        return null;
    }

    [GeneratedRegex(@"(?i)[a-z0-9_@#\-\./\\]+\.(bmp|png|tga|jpg|jpeg|rsm2|rsm|wav|mp3|ogg|str|spr|act)", RegexOptions.Compiled)]
    private static partial Regex ResourceReferenceRegex();
}
