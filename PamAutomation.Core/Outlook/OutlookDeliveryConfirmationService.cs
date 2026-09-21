using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Resilience;

namespace PamAutomation.Core.Outlook;

/// <summary>
/// Observes Outlook after a send request without changing the user's Outbox or creating retries.
/// </summary>
public sealed class OutlookDeliveryConfirmationService
{
    private readonly IOutlookClassicClient outlookClient;
    private readonly IAsyncDelay delay;
    private readonly ILogger logger;
    private readonly int maximumAttempts;
    private readonly TimeSpan pollInterval;

    public OutlookDeliveryConfirmationService(
        IOutlookClassicClient outlookClient,
        IAsyncDelay delay,
        ILogger? logger = null)
        : this(
            outlookClient,
            delay,
            PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts,
            PamOperationTimingPolicy.OutlookConfirmationPollInterval,
            logger)
    {
    }

    internal OutlookDeliveryConfirmationService(
        IOutlookClassicClient outlookClient,
        IAsyncDelay delay,
        int maximumAttempts,
        TimeSpan pollInterval,
        ILogger? logger = null)
    {
        this.outlookClient = outlookClient ?? throw new ArgumentNullException(nameof(outlookClient));
        this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
        if (maximumAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));
        this.maximumAttempts = maximumAttempts;
        this.pollInterval = pollInterval;
        this.logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Waits a bounded period for Outlook to move the message to Sent Items. Transient lookup
    /// failures are retried because repeating this read-only operation cannot duplicate mail.
    /// </summary>
    public async Task<OutlookDeliveryResult> WaitForConfirmationAsync(
        string smtpAddress,
        string subject,
        IReadOnlyList<string> requestIds,
        OutlookDeliveryResult submitted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        OutlookExistingMessageStatus lastStatus = OutlookExistingMessageStatus.None;

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                lastStatus = await outlookClient
                    .FindExistingAsync(smtpAddress, subject, requestIds, cancellationToken)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Outlook post-send observation completed. Attempt={Attempt}, MaximumAttempts={MaximumAttempts}, Status={Status}.",
                    attempt,
                    maximumAttempts,
                    lastStatus);

                switch (lastStatus)
                {
                    case OutlookExistingMessageStatus.Sent:
                        return OutlookDeliveryResult.SentConfirmed();
                    case OutlookExistingMessageStatus.Draft:
                        return OutlookDeliveryResult.DraftedNotSent();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutlookOperationException exception)
            {
                logger.LogWarning(
                    exception,
                    "Outlook post-send observation failed transiently. Attempt={Attempt}, MaximumAttempts={MaximumAttempts}.",
                    attempt,
                    maximumAttempts);
            }

            if (attempt < maximumAttempts)
            {
                await delay.DelayAsync(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        return lastStatus == OutlookExistingMessageStatus.Outbox
            ? OutlookDeliveryResult.OutboxPending()
            : submitted.Status == OutlookDeliveryStatus.SubmittedUnconfirmed
                ? submitted
                : OutlookDeliveryResult.NotConfirmed();
    }
}
