namespace RagnaForge.Infrastructure.Patch;

public enum LuaScriptFormat
{
    Missing = 0,
    Text = 1,
    LuaBytecode = 2,
    BinaryUnknown = 3
}

public sealed record LuaScriptInspectionResult(
    string Path,
    LuaScriptFormat Format,
    bool ReadableAsText,
    string Message);

public sealed class LuaScriptFormatDetector
{
    private static readonly byte[] LuaBytecodeSignature = [0x1B, 0x4C, 0x75, 0x61];

    public LuaScriptInspectionResult Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new LuaScriptInspectionResult(path, LuaScriptFormat.Missing, false, "File was not found.");
        }

        var buffer = File.ReadAllBytes(path);
        if (buffer.Length >= LuaBytecodeSignature.Length
            && buffer.Take(LuaBytecodeSignature.Length).SequenceEqual(LuaBytecodeSignature))
        {
            return new LuaScriptInspectionResult(path, LuaScriptFormat.LuaBytecode, false, "Lua bytecode signature was detected.");
        }

        var sample = buffer.Take(Math.Min(buffer.Length, 2048)).ToArray();
        var zeroBytes = sample.Count(value => value == 0);
        if (zeroBytes > 0)
        {
            return new LuaScriptInspectionResult(path, LuaScriptFormat.BinaryUnknown, false, "Binary-looking content was detected without the standard Lua bytecode signature.");
        }

        var printable = sample.Count(IsTextLikeByte);
        var ratio = sample.Length == 0 ? 1d : (double)printable / sample.Length;
        if (ratio >= 0.85d)
        {
            return new LuaScriptInspectionResult(path, LuaScriptFormat.Text, true, "Content looks like readable text/Lua.");
        }

        return new LuaScriptInspectionResult(path, LuaScriptFormat.BinaryUnknown, false, "Content is not confidently readable as text.");
    }

    private static bool IsTextLikeByte(byte value) =>
        value is 9 or 10 or 13
        || (value >= 32 && value <= 126)
        || value >= 128;
}
