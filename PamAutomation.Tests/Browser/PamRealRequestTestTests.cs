using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
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
