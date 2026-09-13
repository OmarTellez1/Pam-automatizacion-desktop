using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamIntegration")]
[DoNotParallelize]
public sealed class PamRealFavoritesTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_REAL_FAVORITES";
    private const string RequiredOptInValue = "READ_ONLY";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(180_000)]
    public async Task ReadAllAsync_ExplicitReadOnlyAuthorization_ReturnsFavoriteAccounts()
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

        IReadOnlyList<PamFavoriteAccount> accounts = await favorites.ReadAllAsync(
            credentials,
            TestContext.CancellationToken);

        Assert.IsNotEmpty(accounts);
        Assert.IsTrue(accounts.All(account =>
            !string.IsNullOrWhiteSpace(account.SystemName) &&
            !string.IsNullOrWhiteSpace(account.ApplicationName) &&
            !string.IsNullOrWhiteSpace(account.AccountName) &&
            !string.IsNullOrWhiteSpace(account.IpAddress)));
    }
}
