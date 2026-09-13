using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Reads a complete, validated snapshot of PAM Favorites.</summary>
public interface IPamFavoritesReader
{
    /// <summary>Authenticates and reads all Favorites rows without opening request forms.</summary>
    /// <param name="credentials">PAM credentials used only for the temporary authenticated session.</param>
    /// <param name="cancellationToken">Cancels authentication and navigation.</param>
    /// <returns>Every validated Favorites row observed across all pages.</returns>
    Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(
        PamCredentials credentials,
        CancellationToken cancellationToken = default);
}
