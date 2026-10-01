using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Interfaces.Execution;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Validation;

namespace PamAutomation.Core.Execution;

/// <summary>Composes Favorites, submission, Pending and safe test services into UI-ready results.</summary>
public sealed class PamAutomationExecutionService : IPamAutomationExecutionService
{
    private readonly PamFavoritesService favoritesService;
    private readonly PamRequestSubmissionService submissionService;
    private readonly PamPendingService pendingService;
    private readonly PamRequestTestService testService;
    private readonly AppSettingsValidator settingsValidator;
    private readonly TimeProvider timeProvider;
    private readonly IOutlookClassicClient? outlookClient;
    private readonly MailSettingsValidator mailSettingsValidator;
    private readonly OutlookDeliveryConfirmationService? outlookConfirmationService;
    private readonly ILogger<PamAutomationExecutionService> logger;
    private readonly IPendingReconciliationDiagnosticSink? pendingDiagnosticSink;
    private readonly SemaphoreSlim mailGate = new(1, 1);
    private readonly HashSet<string> attemptedMail = new(StringComparer.Ordinal);

    public PamAutomationExecutionService(
        PamFavoritesService favoritesService,
        PamRequestSubmissionService submissionService,
        PamPendingService pendingService,
        PamRequestTestService testService,
        AppSettingsValidator settingsValidator,
        TimeProvider? timeProvider = null,
        IOutlookClassicClient? outlookClient = null,
        MailSettingsValidator? mailSettingsValidator = null,
        ILogger<PamAutomationExecutionService>? logger = null,
        IAsyncDelay? outlookConfirmationDelay = null,
        IPendingReconciliationDiagnosticSink? pendingDiagnosticSink = null)
    {
        this.favoritesService = favoritesService ?? throw new ArgumentNullException(nameof(favoritesService));
        this.submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
        this.pendingService = pendingService ?? throw new ArgumentNullException(nameof(pendingService));
        this.testService = testService ?? throw new ArgumentNullException(nameof(testService));
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.outlookClient = outlookClient;
        this.mailSettingsValidator = mailSettingsValidator ?? new MailSettingsValidator();
        this.logger = logger ?? NullLogger<PamAutomationExecutionService>.Instance;
        this.pendingDiagnosticSink = pendingDiagnosticSink;
        outlookConfirmationService = outlookClient is null
            ? null
            : new OutlookDeliveryConfirmationService(
                outlookClient,
                outlookConfirmationDelay ?? new TaskAsyncDelay(),
                this.logger);
    }

    public async Task<PamExecutionPreparation> PrepareRealAsync(
        PamCredentials credentials,
        AppSettings settings,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default)
        => await PrepareRealCoreAsync(credentials, settings, null, ecuadorNow, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<PamExecutionPreparation> PrepareCollectionRealAsync(
        PamCredentials credentials,
        AppSettings settings,
        IReadOnlyList<FavoriteAccountKey> selectedAccounts,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedAccounts);
        if (selectedAccounts.Count == 0)
        {
            throw new FavoriteCollectionException("La colección activa no contiene solicitudes favoritas.");
        }

        return await PrepareRealCoreAsync(credentials, settings, selectedAccounts, ecuadorNow, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PamExecutionPreparation> PrepareRealCoreAsync(
        PamCredentials credentials,
        AppSettings settings,
        IReadOnlyList<FavoriteAccountKey>? selectedAccounts,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken)
    {
        ValidateInputs(credentials, settings, ecuadorNow);
        PamExecutionAttempt attempt = PamExecutionAttempt.Current ?? new(selectedAccounts?.Count ?? 0, logger);
        using IDisposable attemptScope = attempt.Activate();
        attempt.RecordStage("Favorites");
        logger.LogInformation("Real execution preparation started with Favorites read.");
        IReadOnlyList<PamFavoriteAccount> accounts = await favoritesService
            .ReadAllAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PamFavoriteAccount> executionAccounts = SelectExecutionAccounts(accounts, selectedAccounts);
        PamRequestFormData form = PamRequestFormData.FromSettings(settings.Request);
        attempt.RecordStage("Preflight", expectedCount: executionAccounts.Count);
        string pendingDiagnosticExecutionId = attempt.Capture().AttemptId.ToString("D");
        pendingDiagnosticSink?.Begin(new PendingReconciliationDiagnostic(
            SchemaVersion: 3,
            ExecutionId: pendingDiagnosticExecutionId,
            StartedAtUtc: timeProvider.GetUtcNow(),
            Decision: "ReadingPreflight",
            ExactPendingDuplicateCount: 0,
            ExpectedRequests: executionAccounts
                .Select(account => PendingExpectedRequestDiagnostic.Create(account, form))
                .ToArray(),
            BeforeSubmission: null,
            SubmittedCount: 0,
            Attempts: [],
            Recoveries: [],
            FailureCode: null)
        { Stage = "Preflight" });
        logger.LogInformation("Pending preflight read started before real submissions.");
        IReadOnlyList<PamPendingRequest> pendingBeforeSubmission = await pendingService
            .ReadAllAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        int duplicateCount = pendingService
            .FindExactMatches(pendingBeforeSubmission, executionAccounts, form)
            .Count;
        IReadOnlyList<string> reusableRequestIds = FindReusablePendingRequestIds(
            pendingBeforeSubmission,
            executionAccounts,
            form);
        PendingRequestDiagnostic[] baselineRows = pendingBeforeSubmission
            .Select(PendingRequestDiagnostic.Create)
            .ToArray();
        int baselineRequestIdCount = baselineRows
            .Select(static row => row.RequestId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        pendingDiagnosticSink?.RecordBaseline(new PendingSnapshotDiagnostic(
                timeProvider.GetUtcNow(),
                baselineRows.Length,
                baselineRequestIdCount,
                baselineRows),
            duplicateCount);
        pendingDiagnosticSink?.RecordDecision(
            duplicateCount > 0 ? "AwaitingExistingPendingDecision" : "Submit");
        logger.LogInformation(
            "Real execution preparation completed. FavoriteCount={FavoriteCount}, ExactPendingDuplicateCount={ExactPendingDuplicateCount}, BaselineRequestIdCount={BaselineRequestIdCount}, PendingDiagnosticExecutionId={PendingDiagnosticExecutionId}.",
            executionAccounts.Count,
            duplicateCount,
            baselineRequestIdCount,
            pendingDiagnosticExecutionId);
        return new PamExecutionPreparation(
            executionAccounts.Count,
            settings.Request,
            settings.FinalAction,
            pendingBeforeSubmission.Select(static request => request.RequestId).ToArray(),
            duplicateCount,
            selectedAccounts,
            reusableRequestIds,
            attempt);
    }

    public async Task<PamExecutionResult> ExecuteRealAsync(
        PamCredentials credentials,
        PamExecutionPreparation preparation,
        DateTimeOffset ecuadorNow,
        MailSettings? mailSettings = null,
        CancellationToken cancellationToken = default,
        IProgress<PamRequestSubmissionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(preparation);
        using IDisposable attemptScope = preparation.Attempt.Activate();
        if (preparation.FinalAction == FinalAction.SendEmail)
        {
            if (mailSettings is null) throw new InvalidOperationException("Configura el correo de la colección activa antes de solicitar accesos.");
            mailSettings = mailSettingsValidator.ValidateAndNormalize(mailSettings);
        }
        if (preparation.HasExactPendingDuplicates)
        {
            pendingDiagnosticSink?.RecordDecision("BlockedExistingPending");
            logger.LogWarning(
                "Real execution submission was blocked because exact Pending duplicates exist. DuplicateCount={DuplicateCount}.",
                preparation.ExactPendingDuplicateCount);
            throw new InvalidOperationException(
                "No se permite crear solicitudes exactamente iguales a otras que ya existen en Pending.");
        }

        pendingDiagnosticSink?.RecordDecision("Submit");
        logger.LogInformation("Real execution submission started. FavoriteCount={FavoriteCount}.", preparation.FavoriteCount);

        PamRequestRunAuthorization authorization = PamRequestRunAuthorization.Confirm(
            preparation.FavoriteCount,
            preparation.Request,
            preparation.SelectedAccounts);
        PamRequestRunResult run;
        PamRequestSubmissionException? interrupted = null;
        try
        {
            run = await submissionService.ExecuteAsync(credentials, preparation.Request, ecuadorNow,
                authorization, cancellationToken, progress).ConfigureAwait(false);
        }
        catch (PamRequestSubmissionException exception) when (exception.PartialRun is not null)
        {
            run = exception.PartialRun;
            interrupted = exception;
        }
        if (cancellationToken.IsCancellationRequested && interrupted is null)
        {
            PamExecutionAttempt.Current?.RecordFailureStage();
            interrupted = new PamRequestSubmissionException(PamRequestSubmissionErrorCode.Canceled,
                processedCount: run.SubmittedCount + run.ConflictCount, partialRun: run);
        }
        PamFavoriteAccount[] candidates = run.Accounts
            .Where(result => result.Status is PamRequestSubmissionStatus.Submitted or PamRequestSubmissionStatus.Uncertain)
            .Select(result => result.Account).ToArray();
        pendingDiagnosticSink?.RecordSubmittedCount(run.SubmittedCount);
        var recovered = new HashSet<string>(StringComparer.Ordinal);
        var verifiedGroups = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<string> requestIds = [];
        if (candidates.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            PamExecutionAttempt.Current?.RecordStage("Reconciliation");
            try
            {
                PamPendingComparisonResult comparison = await pendingService.WaitForCompleteComparisonAsync(
                    credentials, candidates, PamRequestFormData.FromSettings(preparation.Request),
                    cancellationToken: cancellationToken,
                    excludedRequestIds: preparation.PendingRequestIdsBeforeSubmission,
                    observation: CaptureUnambiguousGroups).ConfigureAwait(false);
                CaptureUnambiguousGroups(comparison);
                if (!comparison.IsComplete) throw new PamPendingException(PamPendingErrorCode.FieldMismatch);
                requestIds = comparison.RequestIds;
                preparation.ValidateReconciledRequestIds(requestIds, candidates.Length);
            }
            catch (Exception exception)
            {
                PamExecutionAttempt.Current?.RecordFailureStage();
                logger.LogWarning(exception,
                    "Pending recovery stopped. AcceptedCount={AcceptedCount}, UncertainCount={UncertainCount}, RecoveredRequestIdCount={RecoveredRequestIdCount}.",
                    run.SubmittedCount, run.UncertainCount, recovered.Count);
                pendingDiagnosticSink?.RecordFailure(exception is PamPendingException pending ? pending.ErrorCode.ToString() :
                    exception is OperationCanceledException ? "Canceled" : "ReadSessionUnavailable");
                PamExecutionAttempt.Current?.RecordCounts(run.SubmittedCount, run.ConflictCount, run.RejectedCount,
                    run.UncertainCount, run.NotAttemptedCount, recovered.Count, run.FailedBeforeSubmissionCount);
                if (exception is PamPendingException { ErrorCode: PamPendingErrorCode.AmbiguousIdentity }) { recovered.Clear(); verifiedGroups.Clear(); }
                run = WithPendingEvidence();
                PamExecutionAttempt.Current?.RecordCounts(run.SubmittedCount, run.ConflictCount, run.RejectedCount,
                    run.UncertainCount, run.NotAttemptedCount, recovered.Count, run.FailedBeforeSubmissionCount,
                    run.PendingVerifiedCount, run.UnresolvedCount);
                if (PamExecutionAttempt.Current is { } failedAttempt) pendingDiagnosticSink?.RecordSubmissionCounts(failedAttempt.Capture());
                throw new PamRequestSubmissionException(
                    exception is OperationCanceledException ? PamRequestSubmissionErrorCode.Canceled :
                        PamRequestSubmissionErrorCode.ReconciliationFailedAfterSubmission,
                    interrupted ?? exception, run.SubmittedCount + run.ConflictCount, run,
                    recoveredRequestIds: recovered.Order(StringComparer.Ordinal).ToArray(), recoveryStatus: "Incomplete");
            }
        }
        run = WithPendingEvidence();
        PamExecutionAttempt.Current?.RecordCounts(run.SubmittedCount, run.ConflictCount, run.RejectedCount,
                    run.UncertainCount, run.NotAttemptedCount, recovered.Count, run.FailedBeforeSubmissionCount,
                    run.PendingVerifiedCount, run.UnresolvedCount);
        if (interrupted is not null)
        {
            if (PamExecutionAttempt.Current is { } failedAttempt) pendingDiagnosticSink?.RecordSubmissionCounts(failedAttempt.Capture());
            throw new PamRequestSubmissionException(interrupted.ErrorCode, interrupted,
                run.SubmittedCount + run.ConflictCount, run,
                recoveredRequestIds: recovered.Order(StringComparer.Ordinal).ToArray(),
                recoveryStatus: cancellationToken.IsCancellationRequested ? "Canceled" : "ObservedPartial");
        }

        void CaptureUnambiguousGroups(PamPendingComparisonResult comparison)
        {
            // Pending has no Application: a short group cannot assign IDs by row order.
            foreach (var group in candidates.GroupBy(PamPendingService.AccountKey, StringComparer.Ordinal))
            {
                var matches = comparison.Matches.Where(match =>
                    PamPendingService.AccountKey(match.Account) == group.Key).ToArray();
                if (matches.Length != group.Count()) continue;
                verifiedGroups.Add(group.Key);
                foreach (var match in matches) recovered.Add(match.Request.RequestId);
            }
        }

        PamRequestRunResult WithPendingEvidence() => new(run.Accounts.Select(result => result with
        {
            ConfirmedInPending = result.Status is PamRequestSubmissionStatus.Submitted or PamRequestSubmissionStatus.Uncertain &&
                verifiedGroups.Contains(PamPendingService.AccountKey(result.Account))
        }).ToArray());

        OutlookDeliveryResult? delivery = await DeliverIfRequestedAsync(
            preparation.FinalAction,
            mailSettings,
            requestIds,
            isTest: false,
            allowExplicitDuplicateSend: false,
            cancellationToken).ConfigureAwait(false);
        return new PamExecutionResult(
            PamExecutionMode.Real,
            GetEcuadorNow(),
            preparation.FinalAction,
            preparation.FavoriteCount,
            run.Accounts.Count,
            run.ConflictCount,
            requestIds,
            delivery);
    }

    public async Task<PamExecutionResult> ExecuteTestAsync(
        PamCredentials credentials,
        AppSettings settings,
        DateTimeOffset ecuadorNow,
        MailSettings? mailSettings = null,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(credentials, settings, ecuadorNow);
        logger.LogInformation("Non-destructive request validation started.");
        if (!settings.TestModeEnabled)
        {
            throw new InvalidOperationException("El modo de prueba no está habilitado.");
        }

        PamRequestTestResult result = await testService
            .ExecuteAsync(credentials, settings.Request, ecuadorNow, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Non-destructive request validation completed. FavoriteCount={FavoriteCount}, ValidatedCount={ValidatedCount}.",
            result.FavoriteCount, result.ValidatedCount);
        return new PamExecutionResult(
            PamExecutionMode.Test,
            GetEcuadorNow(),
            settings.FinalAction,
            result.FavoriteCount,
            result.ValidatedCount,
            conflictCount: 0,
            requestIds: Array.Empty<string>());
    }

    /// <inheritdoc />
    public async Task<PamExecutionResult> CompleteWithExistingPendingAsync(
        PamExecutionPreparation preparation,
        MailSettings mailSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(mailSettings);
        if (!preparation.CanReusePendingRequestIdsForEmail)
        {
            throw new InvalidOperationException(
                "Pending no contiene un conjunto completo y no ambiguo de Request ID reutilizables.");
        }

        pendingDiagnosticSink?.RecordDecision("UseExistingRequestIds");
        pendingDiagnosticSink?.RecordSubmittedCount(0);

        logger.LogInformation(
            "Existing Pending Request IDs selected for email. RequestIdCount={RequestIdCount}.",
            preparation.ReusablePendingRequestIds.Count);
        OutlookDeliveryResult? delivery = await DeliverIfRequestedAsync(
            preparation.FinalAction,
            mailSettings,
            preparation.ReusablePendingRequestIds,
            isTest: false,
            allowExplicitDuplicateSend: true,
            cancellationToken).ConfigureAwait(false);
        return new PamExecutionResult(
            PamExecutionMode.ExistingPending,
            GetEcuadorNow(),
            preparation.FinalAction,
            preparation.FavoriteCount,
            preparation.FavoriteCount,
            conflictCount: 0,
            preparation.ReusablePendingRequestIds,
            delivery);
    }

    private void ValidateInputs(PamCredentials credentials, AppSettings settings, DateTimeOffset ecuadorNow)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settingsValidator.Validate(settings, ecuadorNow).IsValid)
        {
            throw new InvalidOperationException("La configuración de la aplicación no es válida.");
        }
    }

    private static IReadOnlyList<PamFavoriteAccount> SelectExecutionAccounts(
        IReadOnlyList<PamFavoriteAccount> liveAccounts,
        IReadOnlyList<FavoriteAccountKey>? selectedAccounts)
    {
        if (selectedAccounts is null) return liveAccounts;

        Dictionary<FavoriteAccountKey, PamFavoriteAccount> byKey;
        try
        {
            byKey = liveAccounts.ToDictionary(FavoriteAccountKey.From);
        }
        catch (ArgumentException exception)
        {
            throw new FavoriteCollectionException(
                "Favorites devolvió identidades repetidas y la colección no puede ejecutarse de forma segura.",
                exception);
        }

        var selected = new List<PamFavoriteAccount>(selectedAccounts.Count);
        foreach (FavoriteAccountKey key in selectedAccounts)
        {
            if (!byKey.TryGetValue(key, out PamFavoriteAccount? account))
            {
                throw new FavoriteCollectionException(
                    "Una o más solicitudes de la colección ya no existen en Favorites. Actualiza las solicitudes favoritas antes de continuar.");
            }

            selected.Add(account);
        }

        return selected;
    }

    private IReadOnlyList<string> FindReusablePendingRequestIds(
        IReadOnlyList<PamPendingRequest> pendingRequests,
        IReadOnlyList<PamFavoriteAccount> expectedAccounts,
        PamRequestFormData form)
    {
        try
        {
            PamPendingComparisonResult comparison = pendingService.Compare(
                pendingRequests,
                expectedAccounts,
                form);
            return comparison.IsComplete ? comparison.RequestIds : [];
        }
        catch (PamPendingException exception) when (
            exception.ErrorCode == PamPendingErrorCode.AmbiguousIdentity)
        {
            logger.LogInformation(
                "Existing Pending requests are ambiguous and cannot be reused for email.");
            return [];
        }
    }

    private DateTimeOffset GetEcuadorNow() => timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(-5));

    private async Task<OutlookDeliveryResult?> DeliverIfRequestedAsync(
        FinalAction action,
        MailSettings? mailSettings,
        IReadOnlyList<string> requestIds,
        bool isTest,
        bool allowExplicitDuplicateSend,
        CancellationToken cancellationToken,
        bool distinguishUncertainSubmission = false)
    {
        if (action != FinalAction.SendEmail)
        {
            logger.LogInformation("Email delivery was not requested for this execution.");
            return null;
        }
        if (requestIds.Count == 0)
        {
            logger.LogInformation("Email delivery was skipped because no Request IDs were produced.");
            return OutlookDeliveryResult.SkippedNoRequestIds();
        }
        if (mailSettings is null || outlookClient is null)
            throw new InvalidOperationException("La configuración de correo no está disponible.");

        MailSettings normalized = mailSettingsValidator.ValidateAndNormalize(mailSettings);
        string subject = isTest ? "[PRUEBA] " + normalized.Subject : normalized.Subject;
        string[] to = isTest ? ["tellezoe@hitss.com"] : normalized.ToRecipients.ToArray();
        string[] cc = isTest ? [] : normalized.CcRecipients.ToArray();
        string body = OutlookHtmlComposer.Compose(normalized.HtmlTemplate, requestIds);
        string fingerprint = normalized.SmtpAddress.ToUpperInvariant() + "|" + subject + "|" +
            string.Join(',', requestIds.Order(StringComparer.Ordinal));
        await mailGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool fingerprintWasAdded = attemptedMail.Add(fingerprint);
            if (!allowExplicitDuplicateSend && !fingerprintWasAdded)
            {
                logger.LogWarning("Email delivery was skipped by the in-memory duplicate guard.");
                return OutlookDeliveryResult.DuplicateInMemory();
            }

            bool externalStateMayExist = false;
            try
            {
                if (!allowExplicitDuplicateSend)
                {
                    logger.LogInformation("Outlook external duplicate check started.");
                    OutlookExistingMessageStatus existing = await outlookClient
                        .FindExistingAsync(normalized.SmtpAddress, subject, requestIds, cancellationToken)
                        .ConfigureAwait(false);
                    OutlookDeliveryResult? duplicate = existing switch
                    {
                        OutlookExistingMessageStatus.Sent => OutlookDeliveryResult.DuplicateSentConfirmed(),
                        OutlookExistingMessageStatus.Draft => OutlookDeliveryResult.DuplicateDraft(),
                        OutlookExistingMessageStatus.Outbox => OutlookDeliveryResult.DuplicateOutboxPending(),
                        _ => null
                    };
                    if (duplicate is not null)
                    {
                        externalStateMayExist = true;
                        logger.LogWarning("Email delivery was skipped because Outlook reported status {Status}.", duplicate.Status);
                        return duplicate;
                    }
                }
                else
                {
                    logger.LogInformation(
                        "Explicit existing-Pending email resend selected; duplicate email guards were bypassed by user choice.");
                }

                externalStateMayExist = true;
                logger.LogInformation("Outlook message submission started after duplicate checks.");
                OutlookDeliveryResult submitted = await outlookClient.SendAsync(new OutlookSendRequest(
                    normalized.SmtpAddress, subject, body, to, cc, requestIds)
                {
                    InlineImages = normalized.InlineImages
                }, cancellationToken).ConfigureAwait(false);
                return await outlookConfirmationService!
                    .WaitForConfirmationAsync(
                        normalized.SmtpAddress,
                        subject,
                        requestIds,
                        submitted,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (!externalStateMayExist && fingerprintWasAdded) attemptedMail.Remove(fingerprint);
                logger.LogError(exception, "Email delivery coordination failed. ExternalStateMayExist={ExternalStateMayExist}.", externalStateMayExist);
                if (distinguishUncertainSubmission && externalStateMayExist)
                    throw new OutlookDeliveryUncertainException(exception);
                throw;
            }
        }
        finally
        {
            mailGate.Release();
        }
    }

    public async Task<OutlookDeliveryResult> SendResultEmailAsync(
        MailSettings mailSettings,
        IReadOnlyList<string> requestIds,
        bool isTest,
        CancellationToken cancellationToken = default) =>
        await DeliverIfRequestedAsync(
            FinalAction.SendEmail,
            mailSettings,
            requestIds,
            isTest,
            allowExplicitDuplicateSend: false,
            cancellationToken)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException("No fue posible preparar el correo.");

    public async Task<OutlookDeliveryResult> SendAccumulatedEmailAsync(MailSettings mailSettings,
        IReadOnlyList<string> requestIds, CancellationToken cancellationToken = default) =>
        await DeliverIfRequestedAsync(FinalAction.SendEmail, mailSettings, requestIds, false, false,
            cancellationToken, distinguishUncertainSubmission: true).ConfigureAwait(false)
        ?? throw new InvalidOperationException("No fue posible preparar el correo.");

    public Task<OutlookDeliveryResult> ObserveResultEmailAsync(MailSettings mailSettings,
        IReadOnlyList<string> requestIds, CancellationToken cancellationToken = default)
    {
        if (outlookConfirmationService is null) throw new InvalidOperationException("Outlook no está disponible.");
        return outlookConfirmationService.WaitForConfirmationAsync(mailSettings.SmtpAddress,
            mailSettings.Subject, requestIds, OutlookDeliveryResult.NotConfirmed(), cancellationToken);
    }
}
