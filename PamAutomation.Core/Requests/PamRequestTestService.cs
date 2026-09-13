using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Validation;

namespace PamAutomation.Core.Requests;

/// <summary>Coordinates test-mode form validation while making submission unavailable.</summary>
public sealed class PamRequestTestService
{
    private readonly IPamAuthenticatedSessionFactory authenticatedSessionFactory;
    private readonly PamFavoritesService favoritesService;
    private readonly IPamRequestTestPortalFactory portalFactory;
    private readonly RequestSettingsValidator settingsValidator;

    /// <summary>Initializes the safe request-test workflow.</summary>
    public PamRequestTestService(
        IPamAuthenticatedSessionFactory authenticatedSessionFactory,
        PamFavoritesService favoritesService,
        IPamRequestTestPortalFactory portalFactory,
        RequestSettingsValidator settingsValidator)
    {
        this.authenticatedSessionFactory = authenticatedSessionFactory
            ?? throw new ArgumentNullException(nameof(authenticatedSessionFactory));
        this.favoritesService = favoritesService ?? throw new ArgumentNullException(nameof(favoritesService));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
    }

    /// <summary>Authenticates, validates every favorite form and discards each one.</summary>
    public async Task<PamRequestTestResult> ExecuteAsync(
        PamCredentials credentials,
        RequestSettings settings,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        if (!settingsValidator.Validate(settings, ecuadorNow).IsValid)
        {
            throw new PamRequestTestException(PamRequestTestErrorCode.InvalidSettings);
        }

        await using IPamBrowserSession session = await authenticatedSessionFactory
            .CreateAuthenticatedAsync(credentials, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            IReadOnlyList<PamFavoriteAccount> accounts = await favoritesService
                .ReadAllAsync(session, cancellationToken)
                .ConfigureAwait(false);
            IPamRequestTestPortal portal = portalFactory.Create(session);
            int validatedCount = 0;
            foreach (PamFavoriteAccount account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await portal.ValidateAndDiscardAsync(account, settings, cancellationToken)
                    .ConfigureAwait(false);
                validatedCount++;
            }

            return new PamRequestTestResult(accounts.Count, validatedCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PamRequestTestException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PamRequestTestException(PamRequestTestErrorCode.PortalUnavailable, exception);
        }
    }
}
