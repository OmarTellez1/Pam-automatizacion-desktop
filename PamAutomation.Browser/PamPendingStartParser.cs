using System.Globalization;
using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Parses the English request start value displayed in <c>Pending</c>.</summary>
internal static partial class PamPendingStartParser
{
    private static readonly string[] Formats =
    [
        "MMM dd, yyyy, h:mm tt",
        "MMM d, yyyy, h:mm tt",
        "MMMM dd, yyyy, h:mm tt",
        "MMMM d, yyyy, h:mm tt",
    ];

    public static (DateOnly Date, TimeOnly Time) Parse(string? value)
    {
        string normalized = WhitespaceExpression().Replace(value?.Trim() ?? string.Empty, " ");
        if (!DateTime.TryParseExact(
            normalized,
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out DateTime parsed))
        {
            throw new InvalidOperationException("Pending contiene un Request Start Date inválido.");
        }

        return (DateOnly.FromDateTime(parsed), TimeOnly.FromDateTime(parsed));
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceExpression();
}
