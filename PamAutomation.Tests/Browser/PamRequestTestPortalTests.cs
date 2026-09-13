using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamRequestTestPortalTests
{
    [TestMethod]
    public void Create_ForeignSession_RejectsSession()
    {
        var factory = new PamRequestTestPortalFactory();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(new ForeignSession()));

        Assert.Contains("PamBrowserSessionFactory", exception.Message);
    }

    [TestMethod]
    public async Task ValidateAndDiscardAsync_BoundPage_ForwardsSafeOperation()
    {
        var page = new FakeRequestTestPage();
        IPamRequestTestPortal portal = new PamRequestTestPortal(page);
        var account = new PamFavoriteAccount("system", "application", "account");
        var settings = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 10),
            StartTime = new TimeOnly(9, 0),
            SessionHours = 1,
            RequestReason = "Safe test",
        };

        await portal.ValidateAndDiscardAsync(account, settings, CancellationToken.None);

        Assert.AreSame(account, page.Account);
        Assert.AreSame(settings, page.Settings);
        Assert.AreEqual(1, page.Calls);
    }

    private sealed class FakeRequestTestPage : IPamRequestTestPage
    {
        public int Calls { get; private set; }
        public PamFavoriteAccount? Account { get; private set; }
        public RequestSettings? Settings { get; private set; }

        public Task ValidateAndDiscardRequestFormAsync(
            PamFavoriteAccount account,
            RequestSettings settings,
            CancellationToken cancellationToken)
        {
            Calls++;
            Account = account;
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class ForeignSession : IPamBrowserSession
    {
        public Task<PamNavigationResponse> NavigateAsync(
            Uri uri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
