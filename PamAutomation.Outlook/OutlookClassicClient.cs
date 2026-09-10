using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Outlook;
using PamAutomation.Outlook.Interop;
using System.Runtime.ExceptionServices;

namespace PamAutomation.Outlook;

/// <summary>Implements Outlook classic operations through COM on a dedicated STA thread.</summary>
public sealed class OutlookClassicClient : IOutlookClassicClient
{
    private readonly IOutlookComApplicationFactory applicationFactory;
    private readonly IStaExecutor staExecutor;

    /// <summary>Initializes the production client for the locally installed Outlook classic application.</summary>
    public OutlookClassicClient()
        : this(new OutlookComApplicationFactory(), new StaExecutor())
    {
    }

    internal OutlookClassicClient(IOutlookComApplicationFactory applicationFactory, IStaExecutor staExecutor)
    {
        this.applicationFactory = applicationFactory ?? throw new ArgumentNullException(nameof(applicationFactory));
        this.staExecutor = staExecutor ?? throw new ArgumentNullException(nameof(staExecutor));
    }

    /// <inheritdoc />
    public async Task<OutlookAccount> ResolveAccountAsync(string smtpAddress, CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateSmtpAddress(smtpAddress);
        try
        {
            return await staExecutor.ExecuteAsync(() => ResolveAccount(smtpAddress), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutlookAccountResolutionException)
        {
            throw;
        }
        catch (OutlookOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OutlookOperationException(OutlookOperationErrorCode.AccountResolutionFailed, exception);
        }
    }

    /// <inheritdoc />
    public async Task ValidateAccountAsync(string smtpAddress, CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateSmtpAddress(smtpAddress);
        try
        {
            await staExecutor.ExecuteAsync(() =>
            {
                using IOutlookComApplication application = applicationFactory.Create();
                WithExactAccount(application, smtpAddress, account =>
                {
                    IOutlookComMailItem message = application.CreateMailItem();
                    Exception? primaryFailure = null;
                    try
                    {
                        message.SetSendUsingAccount(account);
                    }
                    catch (Exception exception)
                    {
                        primaryFailure = exception;
                        throw;
                    }
                    finally
                    {
                        DiscardAndDisposeTemporaryMessage(message, primaryFailure);
                    }

                    return 0;
                });
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutlookAccountResolutionException)
        {
            throw;
        }
        catch (OutlookOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OutlookOperationException(OutlookOperationErrorCode.AccountValidationFailed, exception);
        }
    }

    public async Task<OutlookExistingMessageStatus> FindExistingAsync(
        string smtpAddress,
        string subject,
        IReadOnlyList<string> requestIds,
        CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateSmtpAddress(smtpAddress);
        if (string.IsNullOrWhiteSpace(subject))
            throw new OutlookValidationException(OutlookValidationErrorCode.MissingSubject);
        string fingerprint = CreateFingerprint(requestIds);
        try
        {
            return await staExecutor.ExecuteAsync(() =>
            {
                using IOutlookComApplication application = applicationFactory.Create();
                return application.FindExistingMessage(smtpAddress, subject, fingerprint);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (OutlookValidationException) { throw; }
        catch (Exception exception)
        {
            throw new OutlookOperationException(OutlookOperationErrorCode.DuplicateCheckFailed, exception);
        }
    }

    /// <inheritdoc />
    public async Task<OutlookDeliveryResult> SendAsync(OutlookSendRequest request, CancellationToken cancellationToken = default)
    {
        OutlookMessageValidator.ValidateForSend(request);

        try
        {
            return await staExecutor.ExecuteAsync(() => SendMessage(request), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutlookAccountResolutionException)
        {
            throw;
        }
        catch
        {
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

    private static T WithExactAccount<T>(IOutlookComApplication application, string smtpAddress, Func<IOutlookComAccount, T> operation)
    {
        IReadOnlyList<IOutlookComAccount> accounts = application.GetAccounts();
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
