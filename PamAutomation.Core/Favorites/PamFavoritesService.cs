using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Favorites;

/// <summary>Coordinates authenticated, read-only discovery of every PAM favorite account.</summary>
public sealed class PamFavoritesService : IPamFavoritesReader
{
    private readonly IPamAuthenticatedSessionFactory authenticatedSessionFactory;
    private readonly IPamFavoritesPortalFactory portalFactory;

    /// <summary>Initializes the Favorites workflow.</summary>
    /// <param name="authenticatedSessionFactory">Factory for confirmed authenticated sessions.</param>
    /// <param name="portalFactory">Factory for session-bound Favorites readers.</param>
    public PamFavoritesService(
        IPamAuthenticatedSessionFactory authenticatedSessionFactory,
        IPamFavoritesPortalFactory portalFactory)
    {
        this.authenticatedSessionFactory = authenticatedSessionFactory
            ?? throw new ArgumentNullException(nameof(authenticatedSessionFactory));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
    }

    /// <summary>Authenticates, reads Favorites into memory and closes the temporary session.</summary>
    /// <param name="credentials">Protected PAM credentials loaded for this operation.</param>
    /// <param name="cancellationToken">Cancels authentication or read-only navigation.</param>
    /// <returns>A validated in-memory snapshot of all favorite account identities.</returns>
    public async Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(
        PamCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();

        await using IPamBrowserSession session = await authenticatedSessionFactory
            .CreateAuthenticatedAsync(credentials, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await ReadAllAsync(session, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PamFavoritesException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PamFavoritesException(PamFavoritesErrorCode.PortalUnavailable, exception);
        }
    }

    /// <summary>Reads and validates Favorites using an existing authenticated session.</summary>
    /// <param name="session">Authenticated PAM session owned by the caller.</param>
    /// <param name="cancellationToken">Cancels read-only navigation.</param>
    /// <returns>A validated in-memory snapshot of all favorite account identities.</returns>
    public async Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(
        IPamBrowserSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            IPamFavoritesPortal portal = portalFactory.Create(session);
            IReadOnlyList<PamFavoriteAccount> accounts = await portal
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false);
            return Validate(accounts);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PamFavoritesException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PamFavoritesException(PamFavoritesErrorCode.PortalUnavailable, exception);
        }
    }

    private static IReadOnlyList<PamFavoriteAccount> Validate(IReadOnlyList<PamFavoriteAccount>? accounts)
    {
        if (accounts is null)
        {
            throw new PamFavoritesException(PamFavoritesErrorCode.PortalUnavailable);
        }

        if (accounts.Count == 0)
        {
            throw new PamFavoritesException(PamFavoritesErrorCode.NoAccounts);
        }

        var identities = new HashSet<(string System, string Application, string Account)>();
        foreach (PamFavoriteAccount account in accounts)
        {
            if (account is null)
            {
                throw new PamFavoritesException(PamFavoritesErrorCode.InvalidAccount);
            }

            if (!identities.Add((account.SystemName, account.ApplicationName, account.AccountName)))
            {
                throw new PamFavoritesException(PamFavoritesErrorCode.DuplicateAccount);
            }
        }

        return accounts;
    }
}
