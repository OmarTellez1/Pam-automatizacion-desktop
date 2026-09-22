using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Totp;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamReadOnly")]
[DoNotParallelize]
public sealed class PamRealPendingTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_REAL_PENDING";
    private const string RequiredOptInValue = "READ_ONLY";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(300_000)]
    public async Task ReadAllAsync_ExplicitAuthorization_ReadsPendingWithoutMutation()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(OptInVariable),
            RequiredOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        PamCredentials? credentials = new WindowsPamCredentialStore().LoadForAuthentication();
        Assert.IsNotNull(
            credentials,
            "La verificación real requiere la credencial protegida configurada en Windows Credential Manager.");

        var authentication = new PamAuthenticationService(
            new PamBrowserSessionFactory(new VisiblePlaywrightEngineFactory(), new PamOriginPolicy()),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        var service = new PamPendingService(
            authentication,
            new PamPendingPortalFactory(),
            new TaskAsyncDelay());

        await using var session = await authentication.CreateAuthenticatedAsync(credentials, TestContext.CancellationToken);
        for (int read = 1; read <= 3; read++)
        {
            long started = Environment.TickCount64;
            try
            {
                IReadOnlyList<PamPendingRequest> requests = await service.ReadAllAsync(session, TestContext.CancellationToken);
                TestContext.WriteLine("Pending read {0}: {1} rows, {2} ms. No requests created or changed.",
                    read, requests.Count, Environment.TickCount64 - started);
            }
            catch (PamPendingException exception)
            {
                TestContext.WriteLine("Pending failure: {0}; {1}", exception.ErrorCode,
                    System.Text.Json.JsonSerializer.Serialize(exception.BrowserDiagnostic));
                throw;
            }
        }
    }

}
