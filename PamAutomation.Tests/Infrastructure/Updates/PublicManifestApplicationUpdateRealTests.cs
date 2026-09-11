using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Infrastructure.Updates;

namespace PamAutomation.Tests.Infrastructure.Updates;

[TestClass]
[TestCategory("ExplicitUpdateIntegration")]
[DoNotParallelize]
public sealed class PublicManifestApplicationUpdateRealTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_REAL_UPDATE_CHECK";
    private const string RequiredOptInValue = "READ_ONLY";

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CheckAsync_PublicManifest_ReturnsPublishedVersion()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(OptInVariable),
            RequiredOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var service = new PublicManifestApplicationUpdateService(
            httpClient,
            new Uri("https://raw.githubusercontent.com/OmarTellez1/Pam-automation-installer/main/latest.json"),
            new Uri("https://github.com/OmarTellez1/Pam-automation-installer"));

        var result = await service.CheckAsync(new Version(0, 0, 1, 0), TestContext.CancellationToken);

        Assert.IsGreaterThanOrEqualTo(new Version(0, 0, 1, 0), result.LatestVersion);
    }
}
