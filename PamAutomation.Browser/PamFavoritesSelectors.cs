using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Centralizes the stable paths and selectors observed for PAM Favorites.</summary>
internal static partial class PamFavoritesSelectors
{
    public static readonly Uri AccountsUri = new("https://10.30.17.117/webconsole/ps/portal?tab=accounts");
    public const string Favorites = "Favorites";
    public const string FirstPage = "First page";
    public const string NextPage = "Next page";
    public const string Rows = "tr[aria-label^=\"Row \"]";
    public const string PageInput = "input[type=\"number\"][aria-label^=\"Page \"]";
    public const string SystemCell = "td[data-column-id=\"SystemName\"]";
    public const string ApplicationCell = "td[data-column-id=\"ApplicationName\"]";
    public const string AccountCell = "td[data-column-id=\"AccountName\"]";
    public const string IpAddressCell = "td[data-column-id=\"SystemIP\"]";

    [GeneratedRegex("^Browse by Category$", RegexOptions.IgnoreCase)]
    public static partial Regex BrowseByCategoryExpression();
}
