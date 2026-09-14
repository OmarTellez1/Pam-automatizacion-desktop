using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Favorites;
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
        ILogger<PamAutomationExecutionService>? logger = null)
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
        outlookConfirmationService = outlookClient is null
            ? null
            : new OutlookDeliveryConfirmationService(outlookClient, new TaskAsyncDelay(), this.logger);
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
        logger.LogInformation("Real execution preparation started with Favorites read.");
        IReadOnlyList<PamFavoriteAccount> accounts = await favoritesService
            .ReadAllAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PamFavoriteAccount> executionAccounts = SelectExecutionAccounts(accounts, selectedAccounts);
        logger.LogInformation("Pending preflight read started before real submissions.");
        IReadOnlyList<PamPendingRequest> pendingBeforeSubmission = await pendingService
            .ReadAllAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        PamRequestFormData form = PamRequestFormData.FromSettings(settings.Request);
        int duplicateCount = pendingService
            .FindExactMatches(pendingBeforeSubmission, executionAccounts, form)
            .Count;
        logger.LogInformation(
            "Real execution preparation completed. FavoriteCount={FavoriteCount}, ExactPendingDuplicateCount={ExactPendingDuplicateCount}.",
            executionAccounts.Count,
            duplicateCount);
        return new PamExecutionPreparation(
            executionAccounts.Count,
            settings.Request,
            settings.FinalAction,
            pendingBeforeSubmission.Select(static request => request.RequestId).ToArray(),
            duplicateCount,
            selectedAccounts);
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
        logger.LogInformation("Real execution submission started. FavoriteCount={FavoriteCount}.", preparation.FavoriteCount);

        PamRequestRunAuthorization authorization = PamRequestRunAuthorization.Confirm(
            preparation.FavoriteCount,
            preparation.Request,
            preparation.SelectedAccounts);
        PamRequestRunResult run = await submissionService.ExecuteAsync(
            credentials,
            preparation.Request,
            ecuadorNow,
            authorization,
            cancellationToken,
            progress).ConfigureAwait(false);

        PamFavoriteAccount[] submittedAccounts = run.Accounts
            .Where(static result => result.Status == PamRequestSubmissionStatus.Submitted)
            .Select(static result => result.Account)
            .ToArray();
        IReadOnlyList<string> requestIds = Array.Empty<string>();
        if (submittedAccounts.Length > 0)
        {
            logger.LogInformation("Pending reconciliation started. SubmittedCount={SubmittedCount}.", submittedAccounts.Length);
            PamPendingComparisonResult comparison = await pendingService
                .WaitForCompleteComparisonAsync(
                    credentials,
                    submittedAccounts,
                    PamRequestFormData.FromSettings(preparation.Request),
                    cancellationToken: cancellationToken,
                    excludedRequestIds: preparation.PendingRequestIdsBeforeSubmission)
                .ConfigureAwait(false);
            if (!comparison.IsComplete)
            {
                throw new PamPendingException(PamPendingErrorCode.FieldMismatch);
            }

            requestIds = comparison.RequestIds;
            logger.LogInformation("Pending reconciliation completed. RequestIdCount={RequestIdCount}.", requestIds.Count);
        }

        OutlookDeliveryResult? delivery = await DeliverIfRequestedAsync(
            preparation.FinalAction, mailSettings, requestIds, isTest: false, cancellationToken).ConfigureAwait(false);
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

    private DateTimeOffset GetEcuadorNow() => timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(-5));

    private async Task<OutlookDeliveryResult?> DeliverIfRequestedAsync(
        FinalAction action,
        MailSettings? mailSettings,
        IReadOnlyList<string> requestIds,
        bool isTest,
        CancellationToken cancellationToken)
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
            if (!attemptedMail.Add(fingerprint))
            {
                logger.LogWarning("Email delivery was skipped by the in-memory duplicate guard.");
                return OutlookDeliveryResult.DuplicateInMemory();
            }
            bool externalStateMayExist = false;
            try
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
                if (!externalStateMayExist) attemptedMail.Remove(fingerprint);
                logger.LogError(exception, "Email delivery coordination failed. ExternalStateMayExist={ExternalStateMayExist}.", externalStateMayExist);
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
        await DeliverIfRequestedAsync(FinalAction.SendEmail, mailSettings, requestIds, isTest, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException("No fue posible preparar el correo.");
}
