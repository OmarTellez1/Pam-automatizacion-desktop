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
        MailSettingsValidator? mailSettingsValidator = null)
    {
        this.favoritesService = favoritesService ?? throw new ArgumentNullException(nameof(favoritesService));
        this.submissionService = submissionService ?? throw new ArgumentNullException(nameof(submissionService));
        this.pendingService = pendingService ?? throw new ArgumentNullException(nameof(pendingService));
        this.testService = testService ?? throw new ArgumentNullException(nameof(testService));
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.outlookClient = outlookClient;
        this.mailSettingsValidator = mailSettingsValidator ?? new MailSettingsValidator();
    }

    public async Task<PamExecutionPreparation> PrepareRealAsync(
        PamCredentials credentials,
        AppSettings settings,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(credentials, settings, ecuadorNow);
        IReadOnlyList<PamFavoriteAccount> accounts = await favoritesService
            .ReadAllAsync(credentials, cancellationToken)
            .ConfigureAwait(false);
        return new PamExecutionPreparation(accounts.Count, settings.Request, settings.FinalAction);
    }

    public async Task<PamExecutionResult> ExecuteRealAsync(
        PamCredentials credentials,
        PamExecutionPreparation preparation,
        DateTimeOffset ecuadorNow,
        MailSettings? mailSettings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(preparation);

        PamRequestRunAuthorization authorization = PamRequestRunAuthorization.Confirm(
            preparation.FavoriteCount,
            preparation.Request);
        PamRequestRunResult run = await submissionService.ExecuteAsync(
            credentials,
            preparation.Request,
            ecuadorNow,
            authorization,
            cancellationToken).ConfigureAwait(false);

        PamFavoriteAccount[] submittedAccounts = run.Accounts
            .Where(static result => result.Status == PamRequestSubmissionStatus.Submitted)
            .Select(static result => result.Account)
            .ToArray();
        IReadOnlyList<string> requestIds = Array.Empty<string>();
        if (submittedAccounts.Length > 0)
        {
            PamPendingComparisonResult comparison = await pendingService
                .WaitForCompleteComparisonAsync(
                    credentials,
                    submittedAccounts,
                    PamRequestFormData.FromSettings(preparation.Request),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!comparison.IsComplete)
            {
                throw new PamPendingException(PamPendingErrorCode.FieldMismatch);
            }

            requestIds = comparison.RequestIds;
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
        if (!settings.TestModeEnabled)
        {
            throw new InvalidOperationException("El modo de prueba no está habilitado.");
        }

        PamRequestTestResult result = await testService
            .ExecuteAsync(credentials, settings.Request, ecuadorNow, cancellationToken)
            .ConfigureAwait(false);
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

    private DateTimeOffset GetEcuadorNow() => timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(-5));

    private async Task<OutlookDeliveryResult?> DeliverIfRequestedAsync(
        FinalAction action,
        MailSettings? mailSettings,
        IReadOnlyList<string> requestIds,
        bool isTest,
        CancellationToken cancellationToken)
    {
        if (action != FinalAction.SendEmail) return null;
        if (requestIds.Count == 0) return OutlookDeliveryResult.SkippedNoRequestIds();
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
            if (!attemptedMail.Add(fingerprint)) return OutlookDeliveryResult.DuplicateInMemory();
            bool externalStateMayExist = false;
            try
            {
                OutlookExistingMessageStatus existing = await outlookClient
                    .FindExistingAsync(normalized.SmtpAddress, subject, requestIds, cancellationToken)
                    .ConfigureAwait(false);
                OutlookDeliveryResult? duplicate = existing switch
                {
                    OutlookExistingMessageStatus.Sent => OutlookDeliveryResult.DuplicateSentConfirmed(),
                    OutlookExistingMessageStatus.Draft => OutlookDeliveryResult.DuplicateDraft(),
                    OutlookExistingMessageStatus.OutboxMovedToDraft => OutlookDeliveryResult.DuplicateOutboxMovedToDraft(),
                    _ => null
                };
                if (duplicate is not null)
                {
                    externalStateMayExist = true;
                    return duplicate;
                }

                externalStateMayExist = true;
                OutlookDeliveryResult submitted = await outlookClient.SendAsync(new OutlookSendRequest(
                    normalized.SmtpAddress, subject, body, to, cc, requestIds)
                {
                    InlineImages = normalized.InlineImages
                }, cancellationToken).ConfigureAwait(false);
                OutlookExistingMessageStatus afterSend;
                try
                {
                    afterSend = await outlookClient
                        .FindExistingAsync(normalized.SmtpAddress, subject, requestIds, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    return submitted.Status == OutlookDeliveryStatus.SubmittedUnconfirmed
                        ? submitted
                        : OutlookDeliveryResult.NotConfirmed();
                }
                return afterSend switch
                {
                    OutlookExistingMessageStatus.Sent => OutlookDeliveryResult.SentConfirmed(),
                    OutlookExistingMessageStatus.OutboxMovedToDraft => OutlookDeliveryResult.OutboxMovedToDraft(),
                    OutlookExistingMessageStatus.Draft => OutlookDeliveryResult.DuplicateDraft(),
                    _ => submitted
                };
            }
            catch
            {
                if (!externalStateMayExist) attemptedMail.Remove(fingerprint);
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
