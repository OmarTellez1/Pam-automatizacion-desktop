using System.Reflection;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Totp;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamIntegration")]
[DoNotParallelize]
public sealed class PamRealFavoritesNavigationTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(900_000)]
    public async Task Favorites_RealFormsReuseViewAndRecoverAfterRequestsNavigation()
    {
        if (Environment.GetEnvironmentVariable("PAM_AUTOMATION_ALLOW_REAL_REQUEST_FORMS") != "VALIDATE_AND_DISCARD")
            Assert.Inconclusive("Requires explicit safe PAM authorization.");
        var credentials = new WindowsPamCredentialStore().LoadForAuthentication();
        Assert.IsNotNull(credentials);
        var authentication = new PamAuthenticationService(
            new PamBrowserSessionFactory(new VisiblePlaywrightEngineFactory(), new PamOriginPolicy()),
            new PamAuthenticationPortalFactory(), new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(), new TaskAsyncDelay());
        await using var session = (PamBrowserSession)await authentication.CreateAuthenticatedAsync(credentials, TestContext.CancellationToken);
        var managed = (PlaywrightManagedPage)session.FavoritesPage;
        var accounts = await managed.ReadAllFavoritesAsync(TestContext.CancellationToken);
        Assert.IsGreaterThan(1, accounts.Count);
        // Observe browser traffic without exposing the raw page in the application's public API.
        var page = (IPage)typeof(PlaywrightManagedPage).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(IPage)).GetValue(managed)!;
        int accountsLoads = 0;
        int submissions = 0;
        page.Request += (_, request) =>
        {
            if (request.IsNavigationRequest && new Uri(request.Url) == PamFavoritesSelectors.AccountsUri)
                Interlocked.Increment(ref accountsLoads);
            if (request.Method == "POST" && new Uri(request.Url).AbsolutePath.EndsWith("/api/ps-portal/requests", StringComparison.Ordinal))
                Interlocked.Increment(ref submissions);
        };
        // The same adapter used by production is exercised in its blocked-submit mode.
        DateTimeOffset now = TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(AppSettings.EcuadorTimeZoneId));
        var settings = new RequestSettings
        {
            StartDate = DateOnly.FromDateTime(now.DateTime).AddDays(1),
            StartTime = new TimeOnly(9, 0), SessionHours = 1,
            RequestReason = "Validacion segura de navegacion sin envio",
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(14));
        for (int index = 0; index < accounts.Count; index++)
        {
            if (index == accounts.Count / 2)
            {
                // Mimic leaving Favorites for Pending; the next form must restore Favorites once.
                await session.NavigateAsync(new Uri("https://10.30.17.117/webconsole/ps/portal?tab=requests"), timeout.Token);
            }
            await managed.ValidateAndDiscardRequestFormAsync(accounts[index], settings, timeout.Token);
            int expectedLoads = index < accounts.Count / 2 ? 0 : 1;
            Assert.AreEqual(expectedLoads, Volatile.Read(ref accountsLoads), $"Unexpected Accounts reload at form {index + 1}.");
            Assert.AreEqual(0, Volatile.Read(ref submissions));
            TestContext.WriteLine($"Formulario {index + 1}/{accounts.Count} validado y descartado; recargas Accounts: {accountsLoads}; intentos de envio: {submissions}.");
        }
    }
}
