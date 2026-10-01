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
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(900_000)]
    public async Task Calendar_ExplicitSafeAuthorization_SelectsNextMonthAndDiscardsVisibleForms(bool headless)
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
        DateOnly date = new DateOnly(now.Year, now.Month, 1).AddMonths(1);

        // This adapter blocks request creation before opening any form and never clicks Submit.
        for (int index = 0; index < accounts.Count; index++)
        {
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
