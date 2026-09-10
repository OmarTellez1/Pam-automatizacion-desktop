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
            new PamBrowserSessionFactory(),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        string captureDirectory = Path.Combine(FindRepositoryRoot(), "img");
        Directory.CreateDirectory(captureDirectory);
        var service = new PamPendingService(
            authentication,
            new PamPendingPortalFactory(captureDirectory),
            new TaskAsyncDelay());

        IReadOnlyList<PamPendingRequest> requests = await service.ReadAllAsync(
            credentials,
            TestContext.CancellationToken);

        TestContext.WriteLine("Pending rows read: {0}. No requests were created or changed.", requests.Count);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PamAutomation.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio para guardar capturas.");
    }
}
