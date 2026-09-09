using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Creates a Favorites reader bound to one authenticated temporary session.</summary>
public interface IPamFavoritesPortalFactory
{
    /// <summary>Creates the read-only portal adapter for the supplied session.</summary>
    /// <param name="session">Authenticated session that owns the current page.</param>
    /// <returns>A Favorites reader bound exclusively to <paramref name="session"/>.</returns>
    IPamFavoritesPortal Create(IPamBrowserSession session);
}
