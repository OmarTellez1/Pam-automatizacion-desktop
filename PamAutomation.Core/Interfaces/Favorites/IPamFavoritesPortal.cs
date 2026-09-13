using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Defines read-only access to the favorite PAM accounts in one authenticated session.</summary>
public interface IPamFavoritesPortal
{
    /// <summary>Navigates to Favorites and reads every account without opening a request form.</summary>
    /// <param name="cancellationToken">Cancels portal navigation or reading.</param>
    /// <returns>The favorite accounts observed in the portal.</returns>
    Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(CancellationToken cancellationToken);
}
