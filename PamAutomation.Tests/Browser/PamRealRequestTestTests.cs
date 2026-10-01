using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Totp;
using PamAutomation.Core.Validation;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamIntegration")]
[DoNotParallelize]
public sealed class PamRealRequestTestTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_REAL_REQUEST_FORMS";
    private const string RequiredOptInValue = "VALIDATE_AND_DISCARD";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(true, 1)]
    [DataRow(false, 2)]
    [DataRow(false, 3)]
    [DataRow(false, 4)]
    [Timeout(900_000)]
    public async Task Calendar_ExplicitSafeAuthorization_SelectsRequestedMonthAndDiscardsVisibleForms(bool headless, int monthsAhead)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), RequiredOptInValue, StringComparison.Ordinal))
        {
            Assert.Inconclusive("Requires explicit authorization to validate and discard real PAM forms.");
        }

        PamCredentials? credentials = new WindowsPamCredentialStore().LoadForAuthentication();
        Assert.IsNotNull(credentials, "Se requiere la credencial protegida del usuario.");
        var authentication = new PamAuthenticationService(
            new PamBrowserSessionFactory(headless ? new PlaywrightEngineFactory() : new VisiblePlaywrightEngineFactory(), new PamOriginPolicy()),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(14));
        await using var session = await authentication.CreateAuthenticatedAsync(credentials, timeout.Token);
        var favorites = new PamFavoritesService(authentication, new PamFavoritesPortalFactory());
        IReadOnlyList<PamFavoriteAccount> accounts = await favorites.ReadAllAsync(session, timeout.Token);
        Assert.IsGreaterThan(0, accounts.Count);
        var portal = new PamRequestTestPortalFactory().Create(session);
        DateTimeOffset now = TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(AppSettings.EcuadorTimeZoneId));
        // Exercise day 2 of the next month: November 2 in this incident. This fixture
        // does not change the user's requested date or infer why day 1 failed once.
        DateOnly date = new DateOnly(now.Year, now.Month, 1).AddMonths(monthsAhead).AddDays(1);
        // Fixtures use a weekday; never force a day PAM marks as disabled.
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        var attempt = new PamAutomation.Core.Diagnostics.PamExecutionAttempt(accounts.Count);
        using IDisposable attemptScope = attempt.Activate();

        // This adapter blocks request creation before opening any form and never clicks Submit.
        for (int index = 0; index < accounts.Count; index++)
        {
            attempt.RecordStage("Validation", index + 1);
            var settings = new RequestSettings
            {
                StartDate = date,
                StartTime = new TimeOnly(0, 0),
                SessionHours = 8,
                RequestReason = "Validacion segura de calendario sin envio",
            };
            Assert.IsTrue(new RequestSettingsValidator().Validate(settings, now).IsValid);
            try
            {
                await portal.ValidateAndDiscardAsync(accounts[index], settings, timeout.Token);
            }
            catch (PamCalendarSelectionException exception)
            {
                TestContext.WriteLine($"Calendar failure: {exception.Failure}; target: {exception.TargetDate:yyyy-MM-dd}; observed: {exception.ObservedMonth:yyyy-MM}; changes: {exception.NavigationCount}.");
                if (exception.Diagnostic is { } diagnostic)
                {
                    TestContext.WriteLine($"Attempt: {diagnostic.AttemptId}; form: {diagnostic.FormOrdinal}/{diagnostic.ExpectedCount}; observations: {diagnostic.ObservationCount}; read failures: {diagnostic.ReadFailureCount}.");
                    foreach (var sample in diagnostic.History)
                        TestContext.WriteLine($"Calendar: {sample.Stage}/{sample.Condition}; elapsed: {sample.ElapsedMilliseconds}; remaining: {sample.RemainingMilliseconds}; controls: {sample.CalendarCount}/{sample.MonthControlCount}/{sample.YearControlCount}; days: {sample.DayCount}; header: {sample.HeaderMonth:yyyy-MM}; grid: {sample.GridMonth:yyyy-MM}.");
                }
                throw;
            }
            TestContext.WriteLine($"Formulario {index + 1}/{accounts.Count}: fecha {date:yyyy-MM-dd} confirmada y descartada; cero intentos de creación.");
        }
    }

    [TestMethod]
    [Timeout(900_000)]
    public async Task ExecuteAsync_ExplicitSafeAuthorization_ValidatesAllFormsAndSubmitsZero()
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
            "La prueba real requiere una credencial protegida configurada en Windows Credential Manager.");

        var authentication = new PamAuthenticationService(
            new PamBrowserSessionFactory(),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        var favorites = new PamFavoritesService(authentication, new PamFavoritesPortalFactory());
        var service = new PamRequestTestService(
            authentication,
            favorites,
            new PamRequestTestPortalFactory(),
            new RequestSettingsValidator());

        TimeZoneInfo ecuadorTimeZone = TimeZoneInfo.FindSystemTimeZoneById(AppSettings.EcuadorTimeZoneId);
        DateTimeOffset ecuadorNow = TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(), ecuadorTimeZone);
        var settings = new RequestSettings
        {
            StartDate = DateOnly.FromDateTime(ecuadorNow.DateTime).AddDays(1),
            StartTime = new TimeOnly(9, 0),
            SessionHours = 1,
            RequestReason = "Validacion automatizada segura sin envio",
        };

        PamRequestTestResult result = await service.ExecuteAsync(
            credentials,
            settings,
            ecuadorNow,
            TestContext.CancellationToken);

        Assert.IsGreaterThan(0, result.FavoriteCount);
        Assert.AreEqual(result.FavoriteCount, result.ValidatedCount);
        Assert.AreEqual(0, result.SubmittedCount);
        TestContext.WriteLine(
            $"Favorites: {result.FavoriteCount}; validated: {result.ValidatedCount}; submitted: {result.SubmittedCount}.");
    }
}
