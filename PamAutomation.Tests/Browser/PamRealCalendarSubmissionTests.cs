using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Totp;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamMutation")]
[DoNotParallelize]
public sealed class PamRealCalendarSubmissionTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(300_000)]
    public async Task Calendar_AuthorizedThreeSubmissions_ConfirmsIdsWithoutRepeatingSend()
    {
        if (Environment.GetEnvironmentVariable("PAM_AUTOMATION_ALLOW_CALENDAR_SUBMISSIONS") != "CREATE_UP_TO_THREE_FIVE_MINUTE_REQUESTS")
            Assert.Inconclusive("Requires explicit authorization for up to three real five-minute requests.");
        var credentials = new WindowsPamCredentialStore().LoadForAuthentication();
        Assert.IsNotNull(credentials);
        var authentication = new PamAuthenticationService(new PamBrowserSessionFactory(),
            new PamAuthenticationPortalFactory(), new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(), new TaskAsyncDelay());
        await using var session = await authentication.CreateAuthenticatedAsync(credentials, TestContext.CancellationToken);
        var favorites = new PamFavoritesService(authentication, new PamFavoritesPortalFactory());
        var accounts = (await favorites.ReadAllAsync(session, TestContext.CancellationToken)).Take(3).ToArray();
        Assert.HasCount(3, accounts);
        var pending = new PamPendingService(authentication, new PamPendingPortalFactory(), new TaskAsyncDelay());
        var before = await pending.ReadAllAsync(session, TestContext.CancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5));
        var form = new PamRequestFormData(DateOnly.FromDateTime(now.DateTime).AddDays(1),
            new TimeOnly(0, 0), 0, 0, 5, "Prueba autorizada de calendario PAM - 2026-09-30");
        Assert.IsEmpty(pending.FindExactMatches(before, accounts, form),
            "Una ejecución anterior ya creó solicitudes de esta prueba; no se repiten envíos.");
        var portal = new PamRequestSubmissionPortalFactory(captureDirectory: null, maximumSubmissionAttempts: 3).Create(session);
        int confirmed = 0;
        try
        {
            foreach (var account in accounts)
            {
                var outcome = await portal.SubmitAsync(account, form, TestContext.CancellationToken);
                Assert.AreEqual(PamRequestSubmissionStatus.Submitted, outcome.Status);
                confirmed++;
                TestContext.WriteLine($"Envíos confirmados: {confirmed}/3. Fecha: {form.StartDate:yyyy-MM-dd}; duración: 5 minutos.");
            }
            var comparison = await pending.WaitForCompleteComparisonAsync(session, accounts, form,
                cancellationToken: TestContext.CancellationToken,
                excludedRequestIds: before.Select(item => item.RequestId).ToArray());
            Assert.IsTrue(comparison.IsComplete);
            Assert.HasCount(3, comparison.RequestIds);
            TestContext.WriteLine($"Request ID confirmados: {string.Join(", ", comparison.RequestIds)}.");
        }
        catch (PamCalendarSelectionException exception)
        {
            TestContext.WriteLine($"Envíos previos confirmados: {confirmed}; fallo: {exception.Failure}; cambios: {exception.NavigationCount}.");
            var managed = ((PamBrowserSession)session).RequestSubmissionPage;
            var field = managed.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Single(item => item.FieldType == typeof(Microsoft.Playwright.IPage));
            var page = (Microsoft.Playwright.IPage)field.GetValue(managed)!;
            var calendar = page.Locator(".flatpickr-calendar.open:visible");
            TestContext.WriteLine($"Calendarios abiertos: {await calendar.CountAsync()}; días propios visibles: {await calendar.Locator(".flatpickr-day:not(.prevMonthDay):not(.nextMonthDay):not(.hidden):visible").CountAsync()}.");
            throw;
        }
    }
}
