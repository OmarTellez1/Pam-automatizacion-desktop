using System.Net;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitLocalBrowser")]
[DoNotParallelize]
public sealed class PamFavoritesRequestNavigationTests
{
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IPage page = null!;
    private PlaywrightManagedPage managed = null!;
    private int accountsLoads;
    private int accessClicks;
    private int submissionAttempts;
    private int firstClicks;
    private int nextClicks;
    private string? lastAccount;
    private Func<string> freshGrid = () => Grid(Row("wanted"));
    private bool brokenAccess;
    private bool alwaysBrokenAccess;
    private static readonly PamFavoriteAccount Expected = new("system", "application", "wanted");
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public async Task Initialize()
    {
        if (Environment.GetEnvironmentVariable("PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_FAVORITES") != "LOCAL_ONLY")
            Assert.Inconclusive("Requires explicit local browser opt-in.");
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        page = await browser.NewPageAsync();
        page.SetDefaultTimeout(2_000);
        // All URLs, including the production-shaped address, are fulfilled locally.
        await page.RouteAsync("**/*", async route =>
        {
            if (route.Request.Method == "POST")
            {
                submissionAttempts++;
                await route.AbortAsync();
                return;
            }
            Uri uri = new(route.Request.Url);
            string html;
            if (uri.AbsolutePath.StartsWith("/fixture/access/", StringComparison.Ordinal))
            {
                accessClicks++;
                lastAccount = Uri.UnescapeDataString(uri.Segments[^1]);
                html = brokenAccess || alwaysBrokenAccess ? "<p>Temporarily loading</p>" : Form;
                brokenAccess = false;
            }
            else if (uri.AbsolutePath == "/fixture/first")
            {
                firstClicks++;
                html = Grid(Row("wanted"), current: 1, total: 2);
            }
            else if (uri.AbsolutePath == "/fixture/next")
            {
                nextClicks++;
                html = Grid(Row("wanted"), current: 2, total: 2);
            }
            else if (uri == PamFavoritesSelectors.AccountsUri)
            {
                accountsLoads++;
                html = freshGrid();
            }
            else html = "<p>Another view</p>";
            await route.FulfillAsync(new() { ContentType = "text/html", Body = html });
        });
        await page.GotoAsync(PamFavoritesSelectors.AccountsUri.AbsoluteUri);
        managed = new PlaywrightManagedPage(page);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (browser is not null) await browser.CloseAsync();
        playwright?.Dispose();
        Assert.AreEqual(0, submissionAttempts, "Navigation must never submit requests.");
    }

    [TestMethod]
    public async Task ReadyFavorites_ReusesViewForConsecutiveForms()
    {
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(1, accountsLoads);
        Assert.AreEqual("wanted", lastAccount);
        // Simulate the grid restored by a completed/discarded form.
        await page.GotoAsync(PamFavoritesSelectors.AccountsUri.AbsoluteUri);
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(2, accountsLoads, "Only the fixture's explicit return may load Accounts again.");
        Assert.AreEqual(2, accessClicks);
    }

    [TestMethod]
    [DataRow("pending")]
    [DataRow("category")]
    [DataRow("form")]
    public async Task NonReusableView_RestoresAccountsAndFavorites(string state)
    {
        if (state == "pending") await page.GotoAsync("https://10.30.17.117/fixture/pending");
        else await page.SetContentAsync(Grid(Row("wanted"), selected: state != "category") +
            (state == "form" ? Form : ""));
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(2, accountsLoads);
        Assert.AreEqual(1, accessClicks);
    }

    [TestMethod]
    public async Task CurrentPageContainsAccount_DoesNotResetPaginator()
    {
        await page.SetContentAsync(Grid(Row("wanted"), current: 2, total: 2));
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(0, firstClicks);
        Assert.AreEqual(0, nextClicks);
        Assert.AreEqual(1, accountsLoads);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task AccountOnAnotherPage_SearchesRemainingPages(int start)
    {
        await page.SetContentAsync(Grid(Row("other"), current: start, total: 2));
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(start == 2 ? 1 : 0, firstClicks);
        Assert.AreEqual(start == 1 ? 1 : 0, nextClicks);
        Assert.AreEqual("wanted", lastAccount);
        Assert.AreEqual(1, accountsLoads);
    }

    [TestMethod]
    public async Task SameAccountDifferentIdentity_OpensOnlyExactTriple()
    {
        await page.SetContentAsync(Grid(
            Row("wanted", system: "different") + Row("wanted", application: "different") +
            Row("wanted-extra") + Row("wanted")));
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual("wanted", lastAccount);
        Assert.AreEqual(1, accessClicks);
    }

    [TestMethod]
    public async Task ReorderedRows_ResolvesIdentityAgainAtAccessClick()
    {
        await page.SetContentAsync(Grid(Row("wanted").Replace(
            "<a href='/fixture/access/wanted'>Access</a>", "<button disabled>Access</button>") + Row("other")));
        Task preparation = managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        // Simulated render latency: Access initially exists but cannot be clicked.
        await Task.Delay(800, TestContext.CancellationToken);
        await page.SetContentAsync(Grid(Row("other") + Row("wanted")));
        await preparation;
        Assert.AreEqual("wanted", lastAccount);
        Assert.AreEqual(1, accessClicks);
        Assert.AreEqual(1, accountsLoads);
    }

    [TestMethod]
    public async Task DuplicateIdentity_BlocksWithoutReloadOrAccess()
    {
        await page.SetContentAsync(Grid(Row("wanted") + Row("wanted")));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken));
        Assert.AreEqual(1, accountsLoads);
        Assert.AreEqual(0, accessClicks);
    }

    [TestMethod]
    public async Task MissingAccount_ReloadsOnceThenBlocks()
    {
        freshGrid = () => Grid(Row("other"));
        await page.SetContentAsync(freshGrid());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken));
        Assert.AreEqual(2, accountsLoads);
        Assert.AreEqual(0, accessClicks);
    }

    [TestMethod]
    public async Task StaleFilteredView_RecoversExactAccountOnFreshRead()
    {
        await page.SetContentAsync(Grid(Row("other")));
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(2, accountsLoads);
        Assert.AreEqual("wanted", lastAccount);
    }

    [TestMethod]
    [Timeout(45_000)]
    public async Task AccessPanelStalls_RecoversOnceBeforeFilling()
    {
        brokenAccess = true;
        await managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken);
        Assert.AreEqual(2, accountsLoads);
        Assert.AreEqual(2, accessClicks);
    }

    [TestMethod]
    [Timeout(45_000)]
    public async Task PersistentStall_StopsAfterOneRecovery()
    {
        alwaysBrokenAccess = true;
        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            managed.PrepareRequestFormAsync(Expected, TestContext.CancellationToken));
        Assert.AreEqual(2, accountsLoads);
        Assert.AreEqual(2, accessClicks);
    }

    [TestMethod]
    public async Task Cancellation_DoesNotReloadOrOpenAccess()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            managed.PrepareRequestFormAsync(Expected, cancelled.Token));
        Assert.AreEqual(1, accountsLoads);
        Assert.AreEqual(0, accessClicks);
    }

    private static string Grid(string rows, int current = 1, int total = 1, bool selected = true) =>
        $"""
        <div>Browse by Category</div>
        <div class='text {(selected ? "selected" : "")}'><div class='shield focus'><span>Favorites</span></div></div>
        <input type='number' aria-label='Page {current}/{total}' value='{current}'>
        <a role='button' href='/fixture/first'>First page</a><a role='button' href='/fixture/next'>Next page</a>
        <table>{rows}</table>
        """;

    private static string Row(string account, string system = "system", string application = "application") =>
        $"""
        <tr aria-label='Row item'>
        <td data-column-id='SystemName'>{WebUtility.HtmlEncode(system)}</td>
        <td data-column-id='ApplicationName'>{WebUtility.HtmlEncode(application)}</td>
        <td data-column-id='AccountName'>{WebUtility.HtmlEncode(account)}</td>
        <td data-column-id='SystemIP'>192.0.2.1</td>
        <td><a href='/fixture/access/{Uri.EscapeDataString(system == "system" && application == "application" ? account : "wrong")}'>Access</a></td>
        </tr>
        """;

    private const string Form = """
        <button role='tab'>Submit Request</button><h3>Select session start date</h3>
        <label>Start Date<input></label><label>Start Time<input></label><label>Hours<input></label>
        <label>Reason<input></label><button role='switch' aria-checked='true'>Application Session</button>
        <form method='post' action='/api/ps-portal/requests'><button>Submit Request</button></form>
        """;
}
