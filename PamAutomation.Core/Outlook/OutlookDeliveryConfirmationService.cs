using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Resilience;
using System.Diagnostics;

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
        Stopwatch stopwatch = Stopwatch.StartNew();
        int transientFailures = 0;

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool includeUnsentFolders = attempt == maximumAttempts;
            try
            {
                OutlookExistingMessageStatus status = await outlookClient
                    .ObserveDeliveryAsync(smtpAddress, subject, requestIds, includeUnsentFolders, cancellationToken)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Outlook post-send observation completed. Attempt={Attempt}, MaximumAttempts={MaximumAttempts}, Status={Status}, IncludeUnsentFolders={IncludeUnsentFolders}, TransientFailures={TransientFailures}, DurationMs={DurationMs}.",
                    attempt,
                    maximumAttempts,
                    status, includeUnsentFolders, transientFailures, stopwatch.ElapsedMilliseconds);

                switch (status)
                {
                    case OutlookExistingMessageStatus.Sent:
                        return OutlookDeliveryResult.SentConfirmed();
                    case OutlookExistingMessageStatus.Draft when includeUnsentFolders:
                        return OutlookDeliveryResult.DraftedNotSent();
                    case OutlookExistingMessageStatus.Outbox when includeUnsentFolders:
                        return OutlookDeliveryResult.OutboxPending();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutlookOperationException exception) when (exception.IsTransient && attempt < maximumAttempts)
            {
                transientFailures++;
                logger.LogWarning(
                    exception,
                    "Outlook post-send observation will be retried. Attempt={Attempt}, MaximumAttempts={MaximumAttempts}, DurationMs={DurationMs}.",
                    attempt,
                    maximumAttempts, stopwatch.ElapsedMilliseconds);
            }
            catch (OutlookOperationException exception)
            {
                logger.LogError(
                    exception,
                    "Outlook post-send observation stopped without confirmation. Attempt={Attempt}, MaximumAttempts={MaximumAttempts}, IsTransient={IsTransient}, DurationMs={DurationMs}.",
                    attempt, maximumAttempts, exception.IsTransient, stopwatch.ElapsedMilliseconds);
                // Send has already been attempted. Preserve that fact and never initiate another send.
                return UnconfirmedResult(submitted);
            }

            if (attempt < maximumAttempts)
            {
                await delay.DelayAsync(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogWarning(
            "Outlook confirmation window ended without locating the message. MaximumAttempts={MaximumAttempts}, DurationMs={DurationMs}.",
            maximumAttempts, stopwatch.ElapsedMilliseconds);
        return UnconfirmedResult(submitted);
    }

    private static OutlookDeliveryResult UnconfirmedResult(OutlookDeliveryResult submitted) =>
        submitted.Status == OutlookDeliveryStatus.SubmittedUnconfirmed
            ? submitted
            : OutlookDeliveryResult.NotConfirmed();
}
