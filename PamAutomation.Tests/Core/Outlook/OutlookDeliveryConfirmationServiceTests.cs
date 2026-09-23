using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Resilience;

namespace PamAutomation.Tests.Core.Outlook;

[TestClass]
[TestCategory("Unit")]
public sealed class OutlookDeliveryConfirmationServiceTests
{
    [TestMethod]
    public async Task WaitForConfirmationAsync_SlowOutlookEventuallyMovesToSent_ReturnsConfirmed()
    {
        var client = new SequencedOutlookClient(
            OutlookExistingMessageStatus.Outbox,
            OutlookExistingMessageStatus.Outbox,
            OutlookExistingMessageStatus.Sent);
        var delay = new RecordingDelay();
        var service = new OutlookDeliveryConfirmationService(client, delay);

        OutlookDeliveryResult result = await service.WaitForConfirmationAsync(
            "sender@example.test",
            "PAM results",
            ["151901"],
            OutlookDeliveryResult.SubmittedUnconfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.SentConfirmed, result.Status);
        Assert.AreEqual(3, client.LookupCount);
        Assert.AreEqual(2, delay.Delays.Count);
        Assert.IsTrue(delay.Delays.All(static value => value == TimeSpan.FromSeconds(4)));
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_OutboxRemainsPending_DoesNotResendOrTreatAsFailure()
    {
        var client = new SequencedOutlookClient(OutlookExistingMessageStatus.Outbox);
        var delay = new RecordingDelay();
        var service = new OutlookDeliveryConfirmationService(client, delay);

        OutlookDeliveryResult result = await service.WaitForConfirmationAsync(
            "sender@example.test",
            "PAM results",
            ["151901"],
            OutlookDeliveryResult.SubmittedUnconfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.OutboxPending, result.Status);
        Assert.Contains("Bandeja de salida", result.Message);
        Assert.AreEqual(PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts, client.LookupCount);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_TransientLookupErrors_AreRetriedSafely()
    {
        var client = new SequencedOutlookClient(
            new OutlookOperationException(
                OutlookOperationErrorCode.DuplicateCheckFailed,
                new InvalidOperationException("Synthetic transient COM failure."), isTransient: true),
            OutlookExistingMessageStatus.Outbox,
            OutlookExistingMessageStatus.Sent);
        var service = new OutlookDeliveryConfirmationService(client, new RecordingDelay());

        OutlookDeliveryResult result = await service.WaitForConfirmationAsync(
            "sender@example.test",
            "PAM results",
            ["151901"],
            OutlookDeliveryResult.SubmittedUnconfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.SentConfirmed, result.Status);
        Assert.AreEqual(3, client.LookupCount);
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_OutlookMovesMessageToDrafts_ReportsNotSent()
    {
        var client = new SequencedOutlookClient(OutlookExistingMessageStatus.Draft);
        var service = new OutlookDeliveryConfirmationService(client, new RecordingDelay());

        OutlookDeliveryResult result = await service.WaitForConfirmationAsync(
            "sender@example.test",
            "PAM results",
            ["151901"],
            OutlookDeliveryResult.SubmittedUnconfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.DraftedNotSent, result.Status);
        Assert.Contains("Borradores", result.Message);
        Assert.AreEqual(PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts, client.LookupCount);
        Assert.IsTrue(client.ObservedUnsentFolders[^1]);
        Assert.IsTrue(client.ObservedUnsentFolders.SkipLast(1).All(static value => !value));
    }

    private sealed class RecordingDelay : IAsyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_PersistentReadFailure_EndsUnconfirmedWithoutResending()
    {
        var client = new SequencedOutlookClient(new OutlookOperationException(
            OutlookOperationErrorCode.DeliveryObservationFailed, new InvalidOperationException("Synthetic lock."), isTransient: true));
        var delay = new RecordingDelay();
        var service = new OutlookDeliveryConfirmationService(client, delay);

        var result = await service.WaitForConfirmationAsync("sender@example.test", "PAM", ["10"],
            OutlookDeliveryResult.SubmittedUnconfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.SubmittedUnconfirmed, result.Status);
        Assert.AreEqual(PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts, client.LookupCount);
        Assert.AreEqual(PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts - 1, delay.Delays.Count);
        Assert.IsTrue(client.ObservedUnsentFolders[^1]);
        Assert.IsTrue(client.ObservedUnsentFolders.SkipLast(1).All(static value => !value));
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_UnknownFailure_StopsWithoutRetryOrFalseConfirmation()
    {
        var client = new SequencedOutlookClient(new OutlookOperationException(
            OutlookOperationErrorCode.DeliveryObservationFailed, new InvalidOperationException("Unknown failure.")));
        var delay = new RecordingDelay();
        var service = new OutlookDeliveryConfirmationService(client, delay);

        var result = await service.WaitForConfirmationAsync("sender@example.test", "PAM", ["10"],
            OutlookDeliveryResult.NotConfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.NotConfirmed, result.Status);
        Assert.AreEqual(1, client.LookupCount);
        Assert.IsEmpty(delay.Delays);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_NoMessageInAnyFolder_PreservesUnconfirmedState()
    {
        var client = new SequencedOutlookClient(OutlookExistingMessageStatus.None);
        var service = new OutlookDeliveryConfirmationService(client, new RecordingDelay());

        var result = await service.WaitForConfirmationAsync("sender@example.test", "PAM", ["10"],
            OutlookDeliveryResult.SubmittedUnconfirmed());

        Assert.AreEqual(OutlookDeliveryStatus.SubmittedUnconfirmed, result.Status);
        Assert.AreEqual(PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts, client.LookupCount);
        Assert.IsTrue(client.ObservedUnsentFolders[^1]);
        Assert.IsTrue(client.ObservedUnsentFolders.SkipLast(1).All(static value => !value));
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_Cancelled_DoesNotObserveOrSend()
    {
        var client = new SequencedOutlookClient(OutlookExistingMessageStatus.Sent);
        var service = new OutlookDeliveryConfirmationService(client, new RecordingDelay());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.WaitForConfirmationAsync(
            "sender@example.test", "PAM", ["10"], OutlookDeliveryResult.SubmittedUnconfirmed(), cancellation.Token));

        Assert.AreEqual(0, client.LookupCount);
        Assert.AreEqual(0, client.SendCount);
    }

    private sealed class SequencedOutlookClient(params object[] outcomes) : IOutlookClassicClient
    {
        private readonly Queue<object> outcomes = new(outcomes);
        private object? lastOutcome;

        public int LookupCount { get; private set; }
        public int SendCount { get; private set; }
        public List<bool> ObservedUnsentFolders { get; } = [];

        public Task<OutlookExistingMessageStatus> FindExistingAsync(
            string smtpAddress, string subject, IReadOnlyList<string> requestIds,
            CancellationToken cancellationToken = default) =>
            throw new AssertFailedException("Post-send observation must not use the duplicate-check operation.");

        public Task<OutlookExistingMessageStatus> ObserveDeliveryAsync(
            string smtpAddress,
            string subject,
            IReadOnlyList<string> requestIds,
            bool includeUnsentFolders,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObservedUnsentFolders.Add(includeUnsentFolders);
            LookupCount++;
            if (outcomes.Count > 0) lastOutcome = outcomes.Dequeue();
            if (lastOutcome is Exception exception) throw exception;
            OutlookExistingMessageStatus status = (OutlookExistingMessageStatus)(lastOutcome ?? OutlookExistingMessageStatus.None);
            return Task.FromResult(!includeUnsentFolders && status != OutlookExistingMessageStatus.Sent
                ? OutlookExistingMessageStatus.None : status);
        }

        public Task<OutlookAccount> ResolveAccountAsync(string smtpAddress, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OutlookAccount(smtpAddress));

        public Task ValidateAccountAsync(string smtpAddress, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<OutlookDeliveryResult> SendAsync(
            OutlookSendRequest request,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.FromResult(OutlookDeliveryResult.SubmittedUnconfirmed());
        }
    }
}
