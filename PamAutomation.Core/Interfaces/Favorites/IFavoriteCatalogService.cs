using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Loads and refreshes the Favorites catalog used by local collections.</summary>
public interface IFavoriteCatalogService
{
    /// <summary>Loads the last locally protected snapshot without contacting PAM.</summary>
    /// <param name="cancellationToken">Cancels the local read.</param>
    Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads every live Favorites page and replaces the protected local snapshot.</summary>
    /// <param name="cancellationToken">Cancels authentication, navigation or persistence.</param>
    /// <returns>The refreshed snapshot and its difference from the previous snapshot.</returns>
    Task<FavoriteCatalogRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}
