using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Outlook;
using PamAutomation.Outlook;
using PamAutomation.Outlook.Interop;

namespace PamAutomation.Tests.Outlook;

[TestClass]
[TestCategory("Unit")]
public sealed class OutlookClassicClientTests
{
    [TestMethod]
    public async Task ResolveAccountAsync_OneExactMatch_ReturnsConfiguredAccount()
    {
        // Arrange
        OutlookHarness harness = new("other@example.test", "sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        OutlookAccount account = await client.ResolveAccountAsync("SENDER@example.test");

        // Assert
        Assert.AreEqual("sender@example.test", account.SmtpAddress);
        Assert.IsTrue(harness.AllAccountsDisposed);
        Assert.IsTrue(harness.ApplicationDisposed);
    }

    [TestMethod]
    public async Task ResolveAccountAsync_NoMatch_ThrowsStableErrorAndReleasesAccounts()
    {
        // Arrange
        OutlookHarness harness = new("other@example.test");
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        OutlookAccountResolutionException exception = await Assert.ThrowsExactlyAsync<OutlookAccountResolutionException>(() => client.ResolveAccountAsync("sender@example.test"));

        // Assert
        Assert.AreEqual(OutlookAccountResolutionErrorCode.AccountNotFound, exception.ErrorCode);
        Assert.IsTrue(harness.AllAccountsDisposed);
        Assert.IsTrue(harness.ApplicationDisposed);
    }

    [TestMethod]
    public async Task ResolveAccountAsync_MultipleMatches_ThrowsStableErrorAndReleasesAccounts()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test", "SENDER@example.test");
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        OutlookAccountResolutionException exception = await Assert.ThrowsExactlyAsync<OutlookAccountResolutionException>(() => client.ResolveAccountAsync("sender@example.test"));

        // Assert
        Assert.AreEqual(OutlookAccountResolutionErrorCode.MultipleAccountsFound, exception.ErrorCode);
        Assert.IsTrue(harness.AllAccountsDisposed);
        Assert.IsTrue(harness.ApplicationDisposed);
    }

    [TestMethod]
    public async Task ValidateAccountAsync_CreatesTemporaryMessage_AssignsAccountAndDiscardsWithoutSending()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        await client.ValidateAccountAsync("sender@example.test");

        // Assert
        Assert.AreEqual(1, harness.Messages.Count);
        TestMailItem message = harness.Messages[0];
        CollectionAssert.AreEqual(new[] { "account", "discard", "dispose" }, message.Events);
        Assert.AreEqual(0, message.SendCalls);
        Assert.IsTrue(harness.AllResourcesDisposed);
    }

    [TestMethod]
    public async Task SendAsync_ValidRequest_AssignsAccountBeforeSendAndCreatesNewMessagePerOperation()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);
        OutlookSendRequest request = ValidRequest();

        // Act
        OutlookDeliveryResult first = await client.SendAsync(request);
        OutlookDeliveryResult second = await client.SendAsync(request);

        // Assert
        Assert.IsFalse(first.IsConfirmed);
        Assert.IsFalse(second.IsConfirmed);
        Assert.AreEqual(OutlookDeliveryStatus.SubmittedUnconfirmed, first.Status);
        Assert.AreEqual("El correo se envió correctamente desde Outlook.", first.Message);
        Assert.AreEqual(2, harness.Messages.Count);
        CollectionAssert.AreEqual(new[] { "subject", "html", "to", "cc", "account", "fingerprint", "send", "dispose" }, harness.Messages[0].Events);
        Assert.IsTrue(harness.AllResourcesDisposed);
    }

    [TestMethod]
    public async Task SendAsync_ComFailure_ReturnsNotConfirmedAndReleasesResources()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test") { ThrowOnSend = true };
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        OutlookDeliveryResult result = await client.SendAsync(ValidRequest());

        // Assert
        Assert.AreEqual(OutlookDeliveryStatus.NotConfirmed, result.Status);
        Assert.IsTrue(harness.AllResourcesDisposed);
    }

    [TestMethod]
    public async Task ResolveAccountAsync_ComFailure_ThrowsSafeStableErrorWithCause()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test") { ThrowOnGetAccounts = true };
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        OutlookOperationException exception = await Assert.ThrowsExactlyAsync<OutlookOperationException>(() => client.ResolveAccountAsync("sender@example.test"));

        // Assert
        Assert.AreEqual(OutlookOperationErrorCode.AccountResolutionFailed, exception.ErrorCode);
        Assert.AreEqual("No fue posible consultar las cuentas de Outlook.", exception.Message);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.IsTrue(harness.ApplicationDisposed);
    }

    [TestMethod]
    public async Task ValidateAccountAsync_SetAccountFails_DiscardsAndDisposesWithoutReplacingPrimaryFailure()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test") { ThrowOnAccountSetup = true, ThrowOnDiscard = true, ThrowOnDispose = true };
        IOutlookClassicClient client = CreateClient(harness);

        // Act
        OutlookOperationException exception = await Assert.ThrowsExactlyAsync<OutlookOperationException>(() => client.ValidateAccountAsync("sender@example.test"));

        // Assert
        Assert.AreEqual(OutlookOperationErrorCode.AccountValidationFailed, exception.ErrorCode);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        Assert.AreEqual("Synthetic account assignment failure.", exception.InnerException.Message);
        TestMailItem message = harness.Messages[0];
        CollectionAssert.AreEqual(new[] { "account", "discard", "dispose" }, message.Events);
        Assert.AreEqual(0, message.SendCalls);
        Assert.IsTrue(harness.AllResourcesDisposed);
    }

    [TestMethod]
    public async Task SendAsync_CancelledBeforeComOperation_DoesNotCreateApplication()
    {
        // Arrange
        OutlookHarness harness = new("sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        // Act and assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => client.SendAsync(ValidRequest(), cancellation.Token));
        Assert.AreEqual(0, harness.CreateCalls);
    }

    [TestMethod]
    public async Task StaExecutor_RunsOperationInSta()
    {
        // Arrange
        IStaExecutor executor = new StaExecutor();

        // Act
        ApartmentState apartment = await executor.ExecuteAsync(() => Thread.CurrentThread.GetApartmentState(), CancellationToken.None);

        // Assert
        Assert.AreEqual(ApartmentState.STA, apartment);
    }

    [TestMethod]
    public void ValidateForSend_InvalidOrDuplicateRecipients_RejectsBeforeCom()
    {
        // Arrange
        OutlookSendRequest invalid = ValidRequest() with { ToRecipients = ["not-an-address"] };
        OutlookSendRequest duplicate = ValidRequest() with { CcRecipients = ["first@example.test"] };

        // Act and assert
        Assert.AreEqual(OutlookValidationErrorCode.InvalidRecipient, Assert.ThrowsExactly<OutlookValidationException>(() => OutlookMessageValidator.ValidateForSend(invalid)).ErrorCode);
        Assert.AreEqual(OutlookValidationErrorCode.DuplicateRecipient, Assert.ThrowsExactly<OutlookValidationException>(() => OutlookMessageValidator.ValidateForSend(duplicate)).ErrorCode);
    }

    [TestMethod]
    public void ValidateForSend_MissingExpectedRequestId_RejectsBeforeCom()
    {
        // Arrange
        OutlookSendRequest request = ValidRequest() with { ExpectedRequestIds = ["150901", "150902"] };

        // Act
        OutlookValidationException exception = Assert.ThrowsExactly<OutlookValidationException>(() => OutlookMessageValidator.ValidateForSend(request));

        // Assert
        Assert.AreEqual(OutlookValidationErrorCode.MissingRequestId, exception.ErrorCode);
    }

    [TestMethod]
    public async Task FindExistingAsync_UsesOrderIndependentExactFingerprint()
    {
        OutlookHarness harness = new("sender@example.test") { ExistingStatus = OutlookExistingMessageStatus.Sent };
        IOutlookClassicClient client = CreateClient(harness);

        OutlookExistingMessageStatus result = await client.FindExistingAsync("sender@example.test", "Subject", ["20", "10"]);

        Assert.AreEqual(OutlookExistingMessageStatus.Sent, result);
        Assert.AreEqual("10,20", harness.LastFingerprint);
    }

    [TestMethod]
    public async Task SendAsync_InlineImage_AddsCidAttachmentBeforeSend()
    {
        OutlookHarness harness = new("sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);
        OutlookSendRequest request = ValidRequest() with
        {
            HtmlBody = OutlookHtmlComposer.Compose("<p>{{REQUEST_IDS}}</p><img src=\"cid:image@local\" />", ["150901"]),
            InlineImages = [new OutlookInlineImage("image@local", "image.png", "image/png", [1, 2, 3])]
        };

        await client.SendAsync(request);

        CollectionAssert.AreEqual(
            new[] { "subject", "html", "to", "cc", "account", "fingerprint", "image", "send", "dispose" },
            harness.Messages[0].Events);
    }

    [TestMethod]
    public async Task SendAsync_ManualOutlookTestWithoutRequestIds_IsAllowedButHasNoFingerprint()
    {
        OutlookHarness harness = new("sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);
        var request = new OutlookSendRequest(
            "sender@example.test", "Test", "<p>Outlook test</p>", ["sender@example.test"], [], []);

        await client.SendAsync(request);

        Assert.DoesNotContain("fingerprint", harness.Messages[0].Events);
    }

    [TestMethod]
    public async Task SendAsync_UnsafeHtml_RejectsBeforeCreatingComApplication()
    {
        OutlookHarness harness = new("sender@example.test");
        IOutlookClassicClient client = CreateClient(harness);
        OutlookSendRequest request = ValidRequest() with
        {
            HtmlBody = "<p>150901</p><script>active()</script>",
        };

        OutlookValidationException exception = await Assert.ThrowsExactlyAsync<OutlookValidationException>(
            () => client.SendAsync(request));

        Assert.AreEqual(OutlookValidationErrorCode.UnsafeHtmlBody, exception.ErrorCode);
        Assert.AreEqual(0, harness.CreateCalls);
    }

    [TestMethod]
    public void PrivateOutlookModels_ToString_AreRedacted()
    {
        // Arrange
        OutlookSendRequest request = ValidRequest();
        OutlookAccount account = new("sender@example.test");

        // Act
        string requestText = request.ToString();
        string accountText = account.ToString();

        // Assert
        Assert.AreEqual("OutlookSendRequest { Redacted }", requestText);
        Assert.AreEqual("OutlookAccount { Redacted }", accountText);
        Assert.IsFalse(requestText.Contains("150901", StringComparison.Ordinal));
        Assert.IsFalse(requestText.Contains("sender@example.test", StringComparison.Ordinal));
        Assert.IsFalse(accountText.Contains("sender@example.test", StringComparison.Ordinal));
    }

    private static IOutlookClassicClient CreateClient(OutlookHarness harness) => new OutlookClassicClient(harness, new InlineStaExecutor());

    private static OutlookSendRequest ValidRequest() => new(
        "sender@example.test",
        "PAM results",
        OutlookHtmlComposer.Compose("<p>{{REQUEST_IDS}}</p>", ["150901"]),
        ["first@example.test"],
        ["copy@example.test"],
        ["150901"]);

    private sealed class InlineStaExecutor : IStaExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(operation());
        }
    }

    private sealed class OutlookHarness : IOutlookComApplicationFactory, IOutlookComApplication
    {
        private readonly List<TestAccount> accounts;

        public OutlookHarness(params string[] smtpAddresses) => accounts = smtpAddresses.Select(address => new TestAccount(address)).ToList();
        public List<TestMailItem> Messages { get; } = [];
        public bool ThrowOnSend { get; init; }
        public bool ThrowOnGetAccounts { get; init; }
        public bool ThrowOnAccountSetup { get; init; }
        public bool ThrowOnDiscard { get; init; }
        public bool ThrowOnDispose { get; init; }
        public bool ApplicationDisposed { get; private set; }
        public int CreateCalls { get; private set; }
        public OutlookExistingMessageStatus ExistingStatus { get; set; }
        public string? LastFingerprint { get; private set; }
        public bool AllAccountsDisposed => accounts.All(account => account.Disposed);
        public bool AllResourcesDisposed => ApplicationDisposed && AllAccountsDisposed && Messages.All(message => message.Disposed);
        public IOutlookComApplication Create() { CreateCalls++; return this; }
        public IReadOnlyList<IOutlookComAccount> GetAccounts()
        {
            if (ThrowOnGetAccounts) throw new InvalidOperationException("Synthetic account lookup failure.");
            return accounts;
        }
        public IOutlookComMailItem CreateMailItem()
        {
            TestMailItem item = new(ThrowOnSend, ThrowOnAccountSetup, ThrowOnDiscard, ThrowOnDispose);
            Messages.Add(item);
            return item;
        }
        public OutlookExistingMessageStatus FindExistingMessage(string smtpAddress, string subject, string requestFingerprint)
        {
            LastFingerprint = requestFingerprint;
            return ExistingStatus;
        }
        public void Dispose() => ApplicationDisposed = true;
    }

    private sealed class TestAccount(string smtpAddress) : IOutlookComAccount
    {
        public string SmtpAddress { get; } = smtpAddress;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class TestMailItem(bool throwOnSend, bool throwOnAccountSetup, bool throwOnDiscard, bool throwOnDispose) : IOutlookComMailItem
    {
        public List<string> Events { get; } = [];
        public int SendCalls { get; private set; }
        public bool Disposed { get; private set; }
        public void SetSendUsingAccount(IOutlookComAccount account)
        {
            Events.Add("account");
            if (throwOnAccountSetup) throw new InvalidOperationException("Synthetic account assignment failure.");
        }
        public void SetSubject(string subject) => Events.Add("subject");
        public void SetHtmlBody(string htmlBody) => Events.Add("html");
        public void SetToRecipients(IReadOnlyList<string> recipients) => Events.Add("to");
        public void SetCcRecipients(IReadOnlyList<string> recipients) => Events.Add("cc");
        public void SetRequestFingerprint(string requestFingerprint) => Events.Add("fingerprint");
        public void AddInlineImage(OutlookInlineImage image) => Events.Add("image");
        public void Send()
        {
            Events.Add("send");
            SendCalls++;
            if (throwOnSend) throw new InvalidOperationException("Synthetic COM failure.");
        }
        public void Discard()
        {
            Events.Add("discard");
            if (throwOnDiscard) throw new InvalidOperationException("Synthetic discard failure.");
        }
        public void Dispose()
        {
            Disposed = true;
            Events.Add("dispose");
            if (throwOnDispose) throw new InvalidOperationException("Synthetic dispose failure.");
        }
    }
}
