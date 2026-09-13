using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.Browser;

/// <summary>Creates read-only Favorites adapters bound to authenticated temporary sessions.</summary>
public sealed class PamFavoritesPortalFactory : IPamFavoritesPortalFactory
{
    /// <inheritdoc />
    public IPamFavoritesPortal Create(IPamBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session is not PamBrowserSession pamSession)
        {
            throw new ArgumentException(
                "Favorites requiere una sesión creada por PamBrowserSessionFactory.",
                nameof(session));
        }

        return new PamFavoritesPortal(pamSession.FavoritesPage);
    }
}

/// <summary>Maps the Favorites contract to the page owned by one PAM browser session.</summary>
internal sealed class PamFavoritesPortal(IPamFavoritesPage page) : IPamFavoritesPortal
{
    private readonly IPamFavoritesPage page = page ?? throw new ArgumentNullException(nameof(page));

    public Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(CancellationToken cancellationToken) =>
        page.ReadAllFavoritesAsync(cancellationToken);
}
