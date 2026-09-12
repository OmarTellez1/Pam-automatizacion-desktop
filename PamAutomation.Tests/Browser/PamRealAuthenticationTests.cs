using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamIntegration")]
[DoNotParallelize]
public sealed class PamRealAuthenticationTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_REAL_LOGIN";
    private const string RequiredOptInValue = "LOGIN_ONLY";
    private const string InteractiveOptInVariable = "PAM_AUTOMATION_ALLOW_INTERACTIVE_LOGIN";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(120_000)]
    public async Task AuthenticateAsync_ExplicitLoginOnlyAuthorization_ConfirmsDashboard()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(OptInVariable),
            RequiredOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        var credentialStore = new WindowsPamCredentialStore();
        PamCredentials? credentials = credentialStore.LoadForAuthentication();
        Assert.IsNotNull(
            credentials,
            "La prueba real requiere una credencial protegida configurada en Windows Credential Manager.");

        var service = new PamAuthenticationService(
            new PamBrowserSessionFactory(),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());

        await service.AuthenticateAsync(credentials, TestContext.CancellationToken);
    }

    [TestMethod]
    [Timeout(120_000)]
    public async Task InteractiveAccessAsync_ExplicitAuthorization_LeavesAuthenticatedEdgeOpen()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(InteractiveOptInVariable),
            RequiredOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        var credentialStore = new WindowsPamCredentialStore();
        PamCredentials? credentials = credentialStore.LoadForAuthentication();
        Assert.IsNotNull(
            credentials,
            "La prueba real requiere una credencial protegida configurada en Windows Credential Manager.");

        var authentication = new PamAuthenticationService(
            new PamInteractiveBrowserSessionFactory(new WpfPamCertificateTrustPrompt()),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        var access = new PamInteractiveAccessService(authentication);

        await access.OpenAsync(credentials, TestContext.CancellationToken);
        TestContext.WriteLine("Interactive PAM login completed. Edge remains open; no request or email action was executed.");
    }
}
