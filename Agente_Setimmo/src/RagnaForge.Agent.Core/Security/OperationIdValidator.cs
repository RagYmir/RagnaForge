using System.Text.RegularExpressions;

namespace RagnaForge.Agent.Core.Security;

/// <summary>
/// Central validator for operation and rollback IDs.
/// Enforces hexadecimal format and length.
/// </summary>
public static partial class OperationIdValidator
{
    private static readonly Regex IdRegex = MyRegex();

    /// <summary>
    /// Validates if the given ID is a safe hexadecimal string of 12 characters.
    /// Blocks any path traversal or invalid characters.
    /// </summary>
    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return IdRegex.IsMatch(id);
    }

    [GeneratedRegex("^[a-f0-9]{12}$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
