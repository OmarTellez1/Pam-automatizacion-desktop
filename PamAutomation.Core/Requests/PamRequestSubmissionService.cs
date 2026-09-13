using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Validation;

namespace PamAutomation.Core.Requests;

/// <summary>Coordinates a confirmed real request for every current <c>Favorites</c> row.</summary>
public sealed class PamRequestSubmissionService
{
    private readonly IPamAuthenticatedSessionFactory authenticatedSessionFactory;
    private readonly PamFavoritesService favoritesService;
    private readonly IPamRequestSubmissionPortalFactory portalFactory;
    private readonly RequestSettingsValidator settingsValidator;

    /// <summary>Initializes the real submission workflow.</summary>
    public PamRequestSubmissionService(
        IPamAuthenticatedSessionFactory authenticatedSessionFactory,
        PamFavoritesService favoritesService,
        IPamRequestSubmissionPortalFactory portalFactory,
        RequestSettingsValidator settingsValidator)
    {
        this.authenticatedSessionFactory = authenticatedSessionFactory
            ?? throw new ArgumentNullException(nameof(authenticatedSessionFactory));
        this.favoritesService = favoritesService ?? throw new ArgumentNullException(nameof(favoritesService));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
    }

    /// <summary>Submits every current <c>Favorites</c> row after verifying the confirmed count.</summary>
    /// <param name="credentials">Protected PAM credentials loaded for this operation.</param>
    /// <param name="settings">Operational hour-based request configuration.</param>
    /// <param name="ecuadorNow">Current Ecuador time supplied by the caller.</param>
    /// <param name="authorization">Exact count and settings explicitly confirmed by the user.</param>
    /// <param name="cancellationToken">Cancels before the next irreversible submission.</param>
    /// <param name="progress">Optional observer notified after PAM returns a known outcome for one row.</param>
    /// <returns>In-memory submission and conflict outcomes.</returns>
    public async Task<PamRequestRunResult> ExecuteAsync(
        PamCredentials credentials,
        RequestSettings settings,
        DateTimeOffset ecuadorNow,
        PamRequestRunAuthorization authorization,
        CancellationToken cancellationToken = default,
        IProgress<PamRequestSubmissionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();

        if (!settingsValidator.Validate(settings, ecuadorNow).IsValid)
        {
            throw new PamRequestSubmissionException(PamRequestSubmissionErrorCode.InvalidSettings);
        }

        await using IPamBrowserSession session = await authenticatedSessionFactory
            .CreateAuthenticatedAsync(credentials, cancellationToken)
            .ConfigureAwait(false);

        var results = new List<PamRequestAccountResult>();
        try
        {
            IReadOnlyList<PamFavoriteAccount> accounts = await favoritesService
                .ReadAllAsync(session, cancellationToken)
                .ConfigureAwait(false);
            if (!authorization.TryConsume(accounts.Count, settings))
            {
                throw new PamRequestSubmissionException(
                    PamRequestSubmissionErrorCode.AuthorizationMismatch);
            }

            IPamRequestSubmissionPortal portal = portalFactory.Create(session);
            PamRequestFormData form = PamRequestFormData.FromSettings(settings);
            results.Capacity = accounts.Count;
            foreach (PamFavoriteAccount account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PamRequestPortalOutcome outcome = await portal
                    .SubmitAsync(account, form, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new PamRequestAccountResult(account, outcome.Status, outcome.ConflictCode));
                progress?.Report(new PamRequestSubmissionProgress(
                    account,
                    outcome.Status,
                    results.Count,
                    accounts.Count));
            }

            return new PamRequestRunResult(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PamRequestSubmissionException exception)
        {
            if (results.Count == 0) throw;
            throw new PamRequestSubmissionException(exception.ErrorCode, exception, results.Count);
        }
        catch (Exception exception)
        {
            throw new PamRequestSubmissionException(
                PamRequestSubmissionErrorCode.PortalUnavailable,
                exception,
                results.Count);
        }
    }
}
