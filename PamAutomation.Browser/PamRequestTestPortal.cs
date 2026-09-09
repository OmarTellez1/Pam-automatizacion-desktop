using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Browser;

/// <summary>Creates test-only form adapters bound to authenticated temporary sessions.</summary>
public sealed class PamRequestTestPortalFactory : IPamRequestTestPortalFactory
{
    /// <inheritdoc />
    public IPamRequestTestPortal Create(IPamBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session is not PamBrowserSession pamSession)
        {
            throw new ArgumentException(
                "El modo de prueba requiere una sesión creada por PamBrowserSessionFactory.",
                nameof(session));
        }

        return new PamRequestTestPortal(pamSession.RequestTestPage);
    }
}

/// <summary>Maps the non-submitting test contract to one authenticated page.</summary>
internal sealed class PamRequestTestPortal(IPamRequestTestPage page) : IPamRequestTestPortal
{
    private readonly IPamRequestTestPage page = page ?? throw new ArgumentNullException(nameof(page));

    public Task ValidateAndDiscardAsync(
        PamFavoriteAccount account,
        RequestSettings settings,
        CancellationToken cancellationToken) =>
        page.ValidateAndDiscardRequestFormAsync(account, settings, cancellationToken);
}
