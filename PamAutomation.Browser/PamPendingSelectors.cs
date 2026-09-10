using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Centralizes paths and selectors observed for the PAM <c>Pending</c> grid.</summary>
internal static partial class PamPendingSelectors
{
    public static readonly Uri RequestsUri = new("https://10.30.17.117/webconsole/ps/portal?tab=requests");
    public const string Status = "Status";
    public const string Pending = "Pending";
    public const string FirstPage = "First page";
    public const string NextPage = "Next page";
    public const string Rows = "tr[aria-label^=\"Row \"]";
    public const string PageInput = "input[type=\"number\"][aria-label^=\"Page \"]";
    public const string RequestIdCell = "td[data-column-id=\"RequestId\"]";
    public const string SystemCell = "td[data-column-id=\"SystemName\"]";
    public const string AccountCell = "td[data-column-id=\"AccountName\"]";
    public const string ApplicationCell = "td[data-column-id=\"Application\"]";
    public const string ReleaseCell = "td[data-column-id=\"ReleaseDt\"]";
    public const string ResponseStatusCell = "td[data-column-id=\"ResponseStatus\"]";
    public const string ExpiresCell = "td[data-column-id=\"ExpiresDt\"]";
    public const string DescriptionCell = "td[data-column-id=\"Description\"]";

    [GeneratedRegex("^(\\d+)\\s+items?$", RegexOptions.IgnoreCase)]
    public static partial Regex ItemCountExpression();
}
