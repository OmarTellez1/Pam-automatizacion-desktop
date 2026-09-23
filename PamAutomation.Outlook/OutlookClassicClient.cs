using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Outlook;
using PamAutomation.Outlook.Interop;
using System.Runtime.ExceptionServices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PamAutomation.Outlook;

/// <summary>Implements Outlook classic operations through COM on a dedicated STA thread.</summary>
public sealed class OutlookClassicClient : IOutlookClassicClient
{
    private readonly IOutlookComApplicationFactory applicationFactory;
    private readonly IStaExecutor staExecutor;
    private readonly ILogger<OutlookClassicClient> logger;

    /// <summary>Initializes the production client for the locally installed Outlook classic application.</summary>
    public OutlookClassicClient(ILogger<OutlookClassicClient>? logger = null)
        : this(new OutlookComApplicationFactory(logger), new StaExecutor(), logger)
    {
    }

    internal OutlookClassicClient(
        IOutlookComApplicationFactory applicationFactory,
        IStaExecutor staExecutor,
        ILogger<OutlookClassicClient>? logger = null)
    {
        this.applicationFactory = applicationFactory ?? throw new ArgumentNullException(nameof(applicationFactory));
        this.staExecutor = staExecutor ?? throw new ArgumentNullException(nameof(staExecutor));
        this.logger = logger ?? NullLogger<OutlookClassicClient>.Instance;
    }

    /// <inheritdoc />
    public async Task<OutlookAccount> ResolveAccountAsync(string smtpAddress, CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateSmtpAddress(smtpAddress);
        logger.LogInformation("Outlook classic account resolution started.");
        try
        {
            OutlookAccount account = await staExecutor.ExecuteAsync(
                () => ResolveAccount(smtpAddress), cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Outlook classic account resolution completed successfully.");
            return account;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutlookAccountResolutionException exception)
        {
            logger.LogError(exception, "Outlook classic account resolution failed.");
            throw;
        }
        catch (OutlookOperationException exception)
        {
            logger.LogError(exception, "Outlook classic account resolution failed.");
            throw;
        }
        catch (OutlookComUnavailableException exception)
        {
            logger.LogError(exception, "Outlook classic application was unavailable during account resolution.");
            throw new OutlookOperationException(OutlookOperationErrorCode.ApplicationUnavailable, exception);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Outlook classic account resolution failed.");
            throw new OutlookOperationException(OutlookOperationErrorCode.AccountResolutionFailed, exception);
        }
    }

    /// <inheritdoc />
    public async Task ValidateAccountAsync(string smtpAddress, CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateSmtpAddress(smtpAddress);
        string stage = "StaThreadStart";
        logger.LogInformation("Outlook classic account validation started.");
        try
        {
            await staExecutor.ExecuteAsync(() =>
            {
                stage = "ComApplicationCreate";
                using IOutlookComApplication application = applicationFactory.Create();
                stage = "ExactAccountResolution";
                WithExactAccount(application, smtpAddress, account =>
                {
                    stage = "TemporaryMailItemCreate";
                    IOutlookComMailItem message = application.CreateMailItem();
                    Exception? primaryFailure = null;
                    try
                    {
                        stage = "SendUsingAccountAssignment";
                        message.SetSendUsingAccount(account);
                    }
                    catch (Exception exception)
                    {
                        primaryFailure = exception;
                        throw;
                    }
                    finally
                    {
                        if (primaryFailure is null)
                        {
                            stage = "TemporaryMailItemDiscard";
                        }
                        DiscardAndDisposeTemporaryMessage(message, primaryFailure);
                    }

                    return 0;
                });
                return 0;
            }, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Outlook classic account validation completed successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutlookAccountResolutionException exception)
        {
            logger.LogError(exception, "Outlook classic account validation failed at {Stage}.", stage);
            throw;
        }
        catch (OutlookOperationException exception)
        {
            logger.LogError(exception, "Outlook classic account validation failed at {Stage}.", stage);
            throw;
        }
        catch (OutlookComUnavailableException exception)
        {
            logger.LogError(exception, "Outlook classic application was unavailable at {Stage}.", stage);
            throw new OutlookOperationException(OutlookOperationErrorCode.ApplicationUnavailable, exception);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Outlook classic account validation failed at {Stage}.", stage);
            throw new OutlookOperationException(OutlookOperationErrorCode.AccountValidationFailed, exception);
        }
    }

    /// <inheritdoc />
    public Task<OutlookExistingMessageStatus> FindExistingAsync(
        string smtpAddress,
        string subject,
        IReadOnlyList<string> requestIds,
        CancellationToken cancellationToken = default) =>
        LookupMessageAsync(smtpAddress, subject, requestIds, false, false, cancellationToken);

    /// <inheritdoc />
    public Task<OutlookExistingMessageStatus> ObserveDeliveryAsync(
        string smtpAddress,
        string subject,
        IReadOnlyList<string> requestIds,
        bool includeUnsentFolders,
        CancellationToken cancellationToken = default) =>
        LookupMessageAsync(smtpAddress, subject, requestIds, true, !includeUnsentFolders, cancellationToken);

    private async Task<OutlookExistingMessageStatus> LookupMessageAsync(
        string smtpAddress, string subject, IReadOnlyList<string> requestIds,
        bool postSend, bool sentOnly, CancellationToken cancellationToken)
    {
        OutlookMessageValidator.ValidateSmtpAddress(smtpAddress);
        if (string.IsNullOrWhiteSpace(subject))
            throw new OutlookValidationException(OutlookValidationErrorCode.MissingSubject);
        string fingerprint = CreateFingerprint(requestIds);
        string purpose = postSend ? "DeliveryObservation" : "DuplicateCheck";
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Outlook message lookup started. Purpose={Purpose}, SentOnly={SentOnly}, RequestIdCount={RequestIdCount}.",
            purpose, sentOnly, requestIds.Count);
        try
        {
            OutlookExistingMessageStatus status = await staExecutor.ExecuteAsync(() =>
            {
                using IOutlookComApplication application = applicationFactory.Create();
                return application.FindExistingMessage(smtpAddress, subject, fingerprint, sentOnly);
            }, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Outlook message lookup completed. Purpose={Purpose}, Status={Status}, DurationMs={DurationMs}.",
                purpose, status, stopwatch.ElapsedMilliseconds);
            return status;
        }
        catch (OperationCanceledException) { throw; }
        catch (OutlookValidationException) { throw; }
        catch (Exception exception)
        {
            if (exception is OutlookMessageLookupException lookup)
            {
                logger.LogInformation(
                    "Outlook message lookup incomplete. Purpose={Purpose}, Folder={Folder}, Operation={Operation}, CandidateCount={CandidateCount}, CandidateIndex={CandidateIndex}, HResult={HResult}, DurationMs={DurationMs}.",
                    purpose, lookup.Folder, lookup.Operation, lookup.CandidateCount, lookup.CandidateIndex,
                    $"0x{lookup.HResult:X8}", stopwatch.ElapsedMilliseconds);
            }
            // The confirmation service owns post-send warnings/errors to avoid logging one failure twice.
            if (!postSend) logger.LogError(exception, "Outlook duplicate-message lookup failed.");
            throw new OutlookOperationException(
                postSend ? OutlookOperationErrorCode.DeliveryObservationFailed : OutlookOperationErrorCode.DuplicateCheckFailed,
                exception, postSend && IsTransientLookupFailure(exception));
        }
    }

    private static bool IsTransientLookupFailure(Exception exception)
    {
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause is COMException)
            {
                // Retry reads only: Outlook is busy or the submitted message is temporarily locked.
                return cause.HResult is unchecked((int)0x80010001) // RPC_E_CALL_REJECTED
                    or unchecked((int)0x8001010A) // RPC_E_SERVERCALL_RETRYLATER
                    or unchecked((int)0x80040114); // MAPI_E_UNABLE_TO_ABORT (observed during lookup)
            }
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<OutlookDeliveryResult> SendAsync(OutlookSendRequest request, CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateForSend(request);
        logger.LogInformation(
            "Outlook message submission started. ToCount={ToCount}, CcCount={CcCount}, InlineImageCount={InlineImageCount}, RequestIdCount={RequestIdCount}.",
            request.ToRecipients.Count, request.CcRecipients.Count, request.InlineImages.Count, request.ExpectedRequestIds.Count);

        try
        {
            OutlookDeliveryResult result = await staExecutor.ExecuteAsync(
                () => SendMessage(request), cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Outlook message submission completed. Status={Status}.", result.Status);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutlookAccountResolutionException exception)
        {
            logger.LogError(exception, "Outlook message submission failed during account resolution.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Outlook message submission was not confirmed.");
            return OutlookDeliveryResult.NotConfirmed();
        }
    }

    private OutlookAccount ResolveAccount(string smtpAddress)
    {
        using IOutlookComApplication application = applicationFactory.Create();
        return WithExactAccount(application, smtpAddress, account => new OutlookAccount(account.SmtpAddress));
    }

    private OutlookDeliveryResult SendMessage(OutlookSendRequest request)
    {
        using IOutlookComApplication application = applicationFactory.Create();
        return WithExactAccount(application, request.SmtpAddress, account =>
        {
            using IOutlookComMailItem message = application.CreateMailItem();
            message.SetSubject(request.Subject);
            message.SetHtmlBody(request.HtmlBody);
            message.SetToRecipients(request.ToRecipients);
            message.SetCcRecipients(request.CcRecipients);
            message.SetSendUsingAccount(account);
            if (request.ExpectedRequestIds.Count > 0)
            {
                message.SetRequestFingerprint(CreateFingerprint(request.ExpectedRequestIds));
            }

            foreach (OutlookInlineImage image in request.InlineImages)
            {
                message.AddInlineImage(image);
            }

            message.Send();
            return OutlookDeliveryResult.SubmittedUnconfirmed();
        });
    }

    private static string CreateFingerprint(IReadOnlyList<string> requestIds)
    {
        ArgumentNullException.ThrowIfNull(requestIds);
        if (requestIds.Count == 0 || requestIds.Any(static id => string.IsNullOrWhiteSpace(id) || !id.All(char.IsAsciiDigit)))
            throw new OutlookValidationException(OutlookValidationErrorCode.MissingRequestId);
        return string.Join(',', requestIds.Order(StringComparer.Ordinal));
    }

    private static void DiscardAndDisposeTemporaryMessage(IOutlookComMailItem message, Exception? primaryFailure)
    {
        Exception? cleanupFailure = null;
        try
        {
            message.Discard();
        }
        catch (Exception exception)
        {
            cleanupFailure = exception;
        }

        try
        {
            message.Dispose();
        }
        catch (Exception exception)
        {
            cleanupFailure ??= exception;
        }

        if (primaryFailure is null && cleanupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }

    private T WithExactAccount<T>(IOutlookComApplication application, string smtpAddress, Func<IOutlookComAccount, T> operation)
    {
        IReadOnlyList<IOutlookComAccount> accounts = application.GetAccounts();
        logger.LogInformation("Outlook account enumeration completed. AccountCount={AccountCount}.", accounts.Count);
        IOutlookComAccount? selected = null;
        try
        {
            foreach (IOutlookComAccount account in accounts)
            {
                if (!string.Equals(account.SmtpAddress, smtpAddress, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (selected is not null)
                {
                    throw new OutlookAccountResolutionException(OutlookAccountResolutionErrorCode.MultipleAccountsFound);
                }

                selected = account;
            }

            if (selected is null)
            {
                throw new OutlookAccountResolutionException(OutlookAccountResolutionErrorCode.AccountNotFound);
            }

            return operation(selected);
        }
        finally
        {
            foreach (IOutlookComAccount account in accounts)
            {
                account.Dispose();
            }
        }
    }
}
