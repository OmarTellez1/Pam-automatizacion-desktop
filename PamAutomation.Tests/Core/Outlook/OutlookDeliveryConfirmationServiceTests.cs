using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Outlook;

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
        Assert.AreEqual(3, delay.Delays.Count);
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
        Assert.AreEqual(10, client.LookupCount);
        Assert.AreEqual(0, client.SendCount);
    }

    [TestMethod]
    public async Task WaitForConfirmationAsync_TransientLookupErrors_AreRetriedSafely()
    {
        var client = new SequencedOutlookClient(
            new OutlookOperationException(
                OutlookOperationErrorCode.DuplicateCheckFailed,
                new InvalidOperationException("Synthetic transient COM failure.")),
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
        Assert.AreEqual(1, client.LookupCount);
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

    private sealed class SequencedOutlookClient(params object[] outcomes) : IOutlookClassicClient
    {
        private readonly Queue<object> outcomes = new(outcomes);
        private object? lastOutcome;

        public int LookupCount { get; private set; }
        public int SendCount { get; private set; }

        public Task<OutlookExistingMessageStatus> FindExistingAsync(
            string smtpAddress,
            string subject,
            IReadOnlyList<string> requestIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCount++;
            if (outcomes.Count > 0) lastOutcome = outcomes.Dequeue();
            if (lastOutcome is Exception exception) throw exception;
            return Task.FromResult((OutlookExistingMessageStatus)(lastOutcome ?? OutlookExistingMessageStatus.None));
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
