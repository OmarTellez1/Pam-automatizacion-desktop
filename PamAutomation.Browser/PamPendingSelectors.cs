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
    public const string ItemsPerPage = "items per page";
    public const string Rows = "tr[aria-label^=\"Row \"]";
    public const string PageInput = "input[type=\"number\"][aria-label^=\"Page \"]";
    public const string RequestIdColumn = "RequestId";
    public const string SystemColumn = "SystemName";
    public const string AccountColumn = "AccountName";
    public const string ApplicationColumn = "Application";
    public const string ReleaseColumn = "ReleaseDt";
    public const string ResponseStatusColumn = "ResponseStatus";
    public const string ExpiresColumn = "ExpiresDt";
    public const string DescriptionColumn = "Description";
    public const string RequestIdCell = "td[data-column-id=\"" + RequestIdColumn + "\"]";
    public const string SystemCell = "td[data-column-id=\"" + SystemColumn + "\"]";
    public const string AccountCell = "td[data-column-id=\"" + AccountColumn + "\"]";
    public const string ApplicationCell = "td[data-column-id=\"" + ApplicationColumn + "\"]";
    public const string ReleaseCell = "td[data-column-id=\"" + ReleaseColumn + "\"]";
    public const string ResponseStatusCell = "td[data-column-id=\"" + ResponseStatusColumn + "\"]";
    public const string ExpiresCell = "td[data-column-id=\"" + ExpiresColumn + "\"]";
    public const string DescriptionCell = "td[data-column-id=\"" + DescriptionColumn + "\"]";

    public static Regex ItemCountExpression() => PamPendingItemCount.CountExpression();

    [GeneratedRegex("^" + ItemsPerPage + "$", RegexOptions.IgnoreCase)]
    public static partial Regex ItemsPerPageExpression();
}
