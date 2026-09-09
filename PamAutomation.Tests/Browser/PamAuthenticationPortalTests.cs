using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamAuthenticationPortalTests
{
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("Navigation complete.", false)]
    [DataRow(" navigation COMPLETE. ", false)]
    [DataRow("Invalid credentials.", true)]
    public void IsCredentialRejection_PortalAnnouncement_ClassifiesSafely(
        string? alertText,
        bool expected)
    {
        Assert.AreEqual(expected, PamLoginAlertClassifier.IsCredentialRejection(alertText));
    }

    [TestMethod]
    public void Create_ForeignSession_RejectsSession()
    {
        var factory = new PamAuthenticationPortalFactory();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(new ForeignSession()));

        Assert.Contains("PamBrowserSessionFactory", exception.Message);
    }

    [TestMethod]
    public async Task SubmitCredentialsAsync_Accepted_ForwardsValuesAndMapsResult()
    {
        var loginPage = new FakeLoginPage { CredentialsAccepted = true };
        var portal = new PamAuthenticationPortal(loginPage);
        var credentials = new PamCredentials("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP");

        PamCredentialSubmissionResult result = await portal.SubmitCredentialsAsync(credentials, CancellationToken.None);

        Assert.AreEqual(PamCredentialSubmissionResult.Accepted, result);
        Assert.AreEqual("fixture-user", loginPage.Username);
        Assert.AreEqual("fixture-password", loginPage.Password);
    }

    [TestMethod]
    public async Task SubmitCredentialsAsync_Rejected_MapsResult()
    {
        var loginPage = new FakeLoginPage { CredentialsAccepted = false };
        var portal = new PamAuthenticationPortal(loginPage);

        PamCredentialSubmissionResult result = await portal.SubmitCredentialsAsync(
            new PamCredentials("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP"),
            CancellationToken.None);

        Assert.AreEqual(PamCredentialSubmissionResult.Rejected, result);
    }

    [TestMethod]
    public async Task SubmitTotpAsync_ForwardsRedactedCodeAndMapsResult()
    {
        var loginPage = new FakeLoginPage { TotpAccepted = true };
        var portal = new PamAuthenticationPortal(loginPage);
        TotpCode code = new TotpGenerator().Generate("JBSWY3DPEHPK3PXP", DateTimeOffset.UnixEpoch);

        PamTotpSubmissionResult result = await portal.SubmitTotpAsync(code, CancellationToken.None);

        Assert.AreEqual(PamTotpSubmissionResult.Accepted, result);
        Assert.AreEqual(code.Value, loginPage.Totp);
        Assert.AreEqual("TOTP code (redacted)", code.ToString());
    }

    [TestMethod]
    public async Task WaitMethods_ForwardToBoundPage()
    {
        var loginPage = new FakeLoginPage { DashboardVisible = true };
        var portal = new PamAuthenticationPortal(loginPage);

        await portal.WaitForTotpRejectionToDisappearAsync(CancellationToken.None);
        bool dashboardVisible = await portal.WaitForDashboardAsync(CancellationToken.None);

        Assert.IsTrue(loginPage.WaitedForRejectionToDisappear);
        Assert.IsTrue(dashboardVisible);
    }

    private sealed class FakeLoginPage : IPamLoginPage
    {
        public bool CredentialsAccepted { get; init; }
        public bool TotpAccepted { get; init; }
        public bool DashboardVisible { get; init; }
        public string? Username { get; private set; }
        public string? Password { get; private set; }
        public string? Totp { get; private set; }
        public bool WaitedForRejectionToDisappear { get; private set; }

        public Task<bool> SubmitCredentialsAsync(string username, string password, CancellationToken cancellationToken)
        {
            Username = username;
            Password = password;
            return Task.FromResult(CredentialsAccepted);
        }

        public Task<bool> SubmitTotpAsync(string code, CancellationToken cancellationToken)
        {
            Totp = code;
            return Task.FromResult(TotpAccepted);
        }

        public Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken)
        {
            WaitedForRejectionToDisappear = true;
            return Task.CompletedTask;
        }

        public Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken) =>
            Task.FromResult(DashboardVisible);
    }

    private sealed class ForeignSession : IPamBrowserSession
    {
        public Task<PamAutomation.Core.Browser.PamNavigationResponse> NavigateAsync(
            Uri uri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
