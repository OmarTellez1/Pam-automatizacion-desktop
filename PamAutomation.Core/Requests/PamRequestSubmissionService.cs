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
    private readonly PamAutomation.Core.Interfaces.Diagnostics.IPendingReconciliationDiagnosticSink? diagnosticSink;

    /// <summary>Initializes the real submission workflow.</summary>
    public PamRequestSubmissionService(
        IPamAuthenticatedSessionFactory authenticatedSessionFactory,
        PamFavoritesService favoritesService,
        IPamRequestSubmissionPortalFactory portalFactory,
        RequestSettingsValidator settingsValidator,
        PamAutomation.Core.Interfaces.Diagnostics.IPendingReconciliationDiagnosticSink? diagnosticSink = null)
    {
        this.authenticatedSessionFactory = authenticatedSessionFactory
            ?? throw new ArgumentNullException(nameof(authenticatedSessionFactory));
        this.favoritesService = favoritesService ?? throw new ArgumentNullException(nameof(favoritesService));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        this.diagnosticSink = diagnosticSink;
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

        PamAutomation.Core.Diagnostics.PamExecutionAttempt? attempt = PamAutomation.Core.Diagnostics.PamExecutionAttempt.Current;
        IPamBrowserSession? session = null;
        IReadOnlyList<PamFavoriteAccount> accounts = [];
        var results = new List<PamRequestAccountResult>();
        bool enteringRow = false;
        try
        {
            session = await authenticatedSessionFactory.CreateAuthenticatedAsync(credentials, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<PamFavoriteAccount> observedAccounts = await favoritesService.ReadAllAsync(session, cancellationToken).ConfigureAwait(false);
            if (!authorization.TryConsume(observedAccounts, settings, out accounts))
                throw new PamRequestSubmissionException(PamRequestSubmissionErrorCode.AuthorizationMismatch);
            attempt?.RecordStage("Submission", expectedCount: accounts.Count);
            if (attempt is not null) diagnosticSink?.RecordSubmissionCounts(attempt.Capture());
            IPamRequestSubmissionPortal portal = portalFactory.Create(session);
            PamRequestFormData form = PamRequestFormData.FromSettings(settings);
            foreach (PamFavoriteAccount account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt?.RecordStage("Form", results.Count + 1);
                enteringRow = true;
                PamRequestPortalOutcome outcome = await portal.SubmitAsync(account, form, cancellationToken).ConfigureAwait(false);
                results.Add(new PamRequestAccountResult(account, outcome.Status, outcome.ConflictCode));
                enteringRow = false;
                RecordCounts();
                progress?.Report(new PamRequestSubmissionProgress(account, outcome.Status, results.Count, accounts.Count));
            }
            // Disposal is part of the protected region: it cannot discard confirmed outcomes.
            await session.DisposeAsync().ConfigureAwait(false);
            session = null;
            return new PamRequestRunResult(results);
        }
        catch (Exception exception)
        {
            attempt?.RecordFailureStage();
            if (accounts.Count == 0)
            {
                if (exception is OperationCanceledException or PamRequestSubmissionException) throw;
                throw new PamRequestSubmissionException(PamRequestSubmissionErrorCode.PortalUnavailable, exception);
            }
            if (enteringRow)
            {
                PamRequestSubmissionException? failure = exception as PamRequestSubmissionException;
                PamRequestSubmissionStatus status = failure?.ConfirmedOutcome?.Status ??
                    (failure?.IsKnownRejection == true ? PamRequestSubmissionStatus.Rejected :
                     failure?.SubmitMayHaveOccurred == false || exception is PamCalendarSelectionException or PamRequestFormVerificationException
                        ? PamRequestSubmissionStatus.FailedBeforeSubmission
                        : PamRequestSubmissionStatus.Uncertain);
                results.Add(new PamRequestAccountResult(accounts[results.Count], status, failure?.ConfirmedOutcome?.ConflictCode)
                { Evidence = failure?.Evidence });
            }
            while (results.Count < accounts.Count)
                results.Add(new PamRequestAccountResult(accounts[results.Count], PamRequestSubmissionStatus.NotAttempted, null));
            var partial = new PamRequestRunResult(results);
            RecordCounts();
            var code = exception is OperationCanceledException ? PamRequestSubmissionErrorCode.Canceled :
                exception is PamRequestSubmissionException typed ? typed.ErrorCode : PamRequestSubmissionErrorCode.PortalUnavailable;
            throw new PamRequestSubmissionException(code, exception, partial.SubmittedCount + partial.ConflictCount, partial);
        }
        finally
        {
            if (session is not null)
            {
                try { await session.DisposeAsync().ConfigureAwait(false); }
                catch { /* Cleanup must never replace the original failure and its partial evidence. */ }
            }
        }

        void RecordCounts()
        {
            var snapshot = new PamRequestRunResult(results);
            attempt?.RecordCounts(snapshot.SubmittedCount, snapshot.ConflictCount, snapshot.RejectedCount,
                snapshot.UncertainCount, accounts.Count - results.Count + snapshot.NotAttemptedCount,
                failedBeforeSubmission: snapshot.FailedBeforeSubmissionCount);
            if (attempt is not null) diagnosticSink?.RecordSubmissionCounts(attempt.Capture());
        }
    }
}
