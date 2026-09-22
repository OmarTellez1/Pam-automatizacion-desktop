using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Parses the complete count and optional visible range published by Pending.</summary>
internal sealed partial record PamPendingItemCount(int Total, int? First = null, int? Last = null)
{
    internal static PamPendingItemCount Parse(string text)
    {
        Match match = CountExpression().Match(text.Trim());
        if (!match.Success || !int.TryParse(match.Groups["total"].Value, out int total))
            throw new InvalidOperationException("Pending no publicó un contador válido.");

        if (!match.Groups["first"].Success) return new(total);
        if (!int.TryParse(match.Groups["first"].Value, out int first) ||
            !int.TryParse(match.Groups["last"].Value, out int last) ||
            first < (total == 0 ? 0 : 1) || last < first || last > total)
            throw new InvalidOperationException("Pending publicó un rango inválido.");
        return new(total, first, last);
    }

    [GeneratedRegex(@"^(?:(?<first>\d+)\s*[-–]\s*(?<last>\d+)\s+of\s+)?(?<total>\d+)\s+items?$", RegexOptions.IgnoreCase)]
    internal static partial Regex CountExpression();
}
