using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
