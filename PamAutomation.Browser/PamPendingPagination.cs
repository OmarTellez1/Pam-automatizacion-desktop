using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Parses the accessible pagination label used by <c>Pending</c>.</summary>
internal static partial class PamPendingPagination
{
    public static (int CurrentPage, int TotalPages) Parse(string? label)
    {
        Match match = PageExpression().Match(label?.Trim() ?? string.Empty);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out int currentPage) ||
            !int.TryParse(match.Groups[2].Value, out int totalPages) ||
            currentPage < 1 ||
            totalPages < 1 ||
            currentPage > totalPages)
        {
            throw new InvalidOperationException("El paginador de Pending no tiene un formato válido.");
        }

        return (currentPage, totalPages);
    }

    [GeneratedRegex("^Page\\s+(\\d+)/(\\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PageExpression();
}
