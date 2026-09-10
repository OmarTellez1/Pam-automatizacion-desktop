using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class CredentialConfigurationWorkflowTests
{
    [TestMethod]
    public async Task SaveAndTestPamAsync_CompleteUpdate_SavesAuthenticatesAndDisposesSession()
    {
        var credentials = new FakeCredentialStore();
        var authentication = new FakeAuthenticationFactory();
        var workflow = CreateWorkflow(credentials, authentication);

        ConfigurationOperationResult result = await workflow.SaveAndTestPamAsync(new PamCredentialUpdate
        {
            Username = "user",
            Password = "password",
            TotpSeed = "JBSWY3DPEHPK3PXP"
        });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, credentials.SaveCount);
        Assert.AreEqual(1, authentication.CreateCount);
        Assert.IsTrue(authentication.SessionDisposed);
    }

    [TestMethod]
    public async Task SaveAndTestPamAsync_AuthenticationFails_ReturnsSafeFailureAfterSaving()
    {
        var credentials = new FakeCredentialStore();
        var authentication = new FakeAuthenticationFactory { Failure = new InvalidOperationException("sensitive") };
        var workflow = CreateWorkflow(credentials, authentication);

        ConfigurationOperationResult result = await workflow.SaveAndTestPamAsync(new PamCredentialUpdate());

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(1, credentials.SaveCount);
        Assert.DoesNotContain("sensitive", result.Message);
    }

    [TestMethod]
    public async Task SaveAndVerifyOutlookAsync_ValidAccount_VerifiesBeforeProtecting()
    {
        var events = new List<string>();
        var outlookStore = new FakeOutlookAccountStore(events);
        var outlook = new FakeOutlookClient(events);
        var workflow = CreateWorkflow(outlookStore: outlookStore, outlook: outlook);

        ConfigurationOperationResult result = await workflow.SaveAndVerifyOutlookAsync(" Sender@Example.test ");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Sender@Example.test", outlook.ValidatedAddress);
        Assert.AreEqual("Sender@Example.test", outlookStore.Saved?.SmtpAddress);
        CollectionAssert.AreEqual(new[] { "validate", "save" }, events);
    }

    [TestMethod]
    public async Task SaveAndVerifyOutlookAsync_InvalidAddress_DoesNotCallOutlookOrStore()
    {
        var outlookStore = new FakeOutlookAccountStore();
        var outlook = new FakeOutlookClient();
        var workflow = CreateWorkflow(outlookStore: outlookStore, outlook: outlook);

        ConfigurationOperationResult result = await workflow.SaveAndVerifyOutlookAsync("not an address");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(outlook.ValidatedAddress);
        Assert.IsNull(outlookStore.Saved);
    }

    [TestMethod]
    public async Task SendOutlookTestAsync_ConfiguredAccount_SendsOnlyFromAndToSameAddress()
    {
        var outlook = new FakeOutlookClient();
        var workflow = CreateWorkflow(outlook: outlook);

        ConfigurationOperationResult result = await workflow.SendOutlookTestAsync("sender@example.test");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(outlook.SentRequest);
        Assert.AreEqual("sender@example.test", outlook.SentRequest.SmtpAddress);
        CollectionAssert.AreEqual(new[] { "sender@example.test" }, outlook.SentRequest.ToRecipients.ToArray());
        Assert.IsEmpty(outlook.SentRequest.CcRecipients);
        Assert.IsEmpty(outlook.SentRequest.ExpectedRequestIds);
    }

    private static CredentialConfigurationWorkflow CreateWorkflow(
        FakeCredentialStore? credentials = null,
        FakeAuthenticationFactory? authentication = null,
        FakeOutlookAccountStore? outlookStore = null,
        FakeOutlookClient? outlook = null) =>
        new(
            credentials ?? new FakeCredentialStore(),
            authentication ?? new FakeAuthenticationFactory(),
            outlookStore ?? new FakeOutlookAccountStore(),
            outlook ?? new FakeOutlookClient());

    private sealed class FakeCredentialStore : IPamCredentialStore
    {
        public int SaveCount { get; private set; }
        public bool HasCredentials() => true;
        public PamCredentials? LoadForAuthentication() => new("user", "password", "JBSWY3DPEHPK3PXP");
        public void Save(PamCredentialUpdate update) => SaveCount++;
        public void Delete() { }
    }

    private sealed class FakeAuthenticationFactory : IPamAuthenticatedSessionFactory
    {
        public int CreateCount { get; private set; }
        public bool SessionDisposed { get; private set; }
        public Exception? Failure { get; init; }

        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return Failure is null
                ? Task.FromResult<IPamBrowserSession>(new FakeSession(() => SessionDisposed = true))
                : Task.FromException<IPamBrowserSession>(Failure);
        }
    }

    private sealed class FakeSession(Action dispose) : IPamBrowserSession
    {
        public Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeOutlookAccountStore(List<string>? events = null) : IOutlookAccountSettingsStore
    {
        public OutlookAccountSettings? Saved { get; private set; }
        public bool Exists() => Saved is not null;
        public Task<OutlookAccountSettings?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved);
        public Task SaveAsync(OutlookAccountSettings settings, CancellationToken cancellationToken = default)
        {
            events?.Add("save");
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutlookClient(List<string>? events = null) : IOutlookClassicClient
    {
        public string? ValidatedAddress { get; private set; }
        public OutlookSendRequest? SentRequest { get; private set; }

        public Task<OutlookAccount> ResolveAccountAsync(string smtpAddress, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OutlookAccount(smtpAddress));

        public Task ValidateAccountAsync(string smtpAddress, CancellationToken cancellationToken = default)
        {
            events?.Add("validate");
            ValidatedAddress = smtpAddress;
            return Task.CompletedTask;
        }

        public Task<OutlookExistingMessageStatus> FindExistingAsync(
            string smtpAddress,
            string subject,
            IReadOnlyList<string> requestIds,
            CancellationToken cancellationToken = default) => Task.FromResult(OutlookExistingMessageStatus.None);

        public Task<OutlookDeliveryResult> SendAsync(
            OutlookSendRequest request,
            CancellationToken cancellationToken = default)
        {
            SentRequest = request;
            return Task.FromResult(OutlookDeliveryResult.SubmittedUnconfirmed());
        }
    }
}
