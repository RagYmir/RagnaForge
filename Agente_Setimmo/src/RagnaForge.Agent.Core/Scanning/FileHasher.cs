using System.Security.Cryptography;

namespace RagnaForge.Agent.Core.Scanning;

/// <summary>
/// Calculates SHA-256 hashes by streaming — never loads entire file into memory.
/// Respects a configurable maximum file size limit.
/// </summary>
public static class FileHasher
{
    /// <summary>
    /// Default maximum file size for hashing: 20 MB.
    /// Files larger than this are skipped with reason "file_too_large".
    /// </summary>
    public const long DefaultMaxFileSizeBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Result of a hash operation.
    /// </summary>
    public sealed class HashResult
    {
        public bool Success { get; init; }
        public string? Hash { get; init; }
        public string? SkipReason { get; init; }

        public static HashResult Ok(string hash) => new() { Success = true, Hash = hash };
        public static HashResult Skipped(string reason) => new() { Success = false, SkipReason = reason };
    }

    /// <summary>
    /// Compute SHA-256 hash of a file by streaming.
    /// Returns a skip result if the file exceeds maxFileSizeBytes.
    /// </summary>
    public static HashResult ComputeSha256(string filePath, long maxFileSizeBytes = DefaultMaxFileSizeBytes)
    {
        try
        {
            var info = new FileInfo(filePath);

            if (!info.Exists)
                return HashResult.Skipped("file_not_found");

            if (info.Length > maxFileSizeBytes)
                return HashResult.Skipped("file_too_large");

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 8192, FileOptions.SequentialScan);
            var hashBytes = SHA256.HashData(stream);

            return HashResult.Ok(Convert.ToHexStringLower(hashBytes));
        }
        catch (UnauthorizedAccessException)
        {
            return HashResult.Skipped("access_denied");
        }
        catch (IOException)
        {
            return HashResult.Skipped("io_error");
        }
    }
}
