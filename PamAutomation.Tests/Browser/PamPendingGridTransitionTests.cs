using System.Text;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Browser;

[TestClass]
[DoNotParallelize]
public sealed class PamPendingGridTransitionTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("30 items", 30)]
    [DataRow("1-25 of 30 items", 30)]
    [DataRow("26–30 of 30 items", 30)]
    [DataRow("0 items", 0)]
    [DataRow("0-0 of 0 items", 0)]
    public void Count_ActualPortalFormats_ReadsTotal(string label, int expected) =>
        Assert.AreEqual(expected, PamPendingItemCount.Parse(label).Total);

    [TestMethod]
    [DataRow("26-31 of 30 items")]
    [DataRow("0-25 of 30 items")]
    [DataRow("30-25 of 30 items")]
    [DataRow("unknown")]
    public void Count_InvalidRange_DoesNotAuthorizePartialRead(string label) =>
        Assert.ThrowsExactly<InvalidOperationException>(() => PamPendingItemCount.Parse(label));

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(60_000)]
    public async Task ReadAllAsync_DelayedResizeFrom25To30_DoesNotClickStaleNextPage()
    {
        if (!LocalBrowserEnabled()) return;
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        int nextClicks = 0;
        await page.RouteAsync("**/*", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string html = path switch
            {
                "/fixture/pending" => Grid(25, 30, 1, 2, 25, sizeOption: true),
                "/fixture/large" => Grid(25, 30, 1, 2, null)
                    .Replace("<button aria-label='Status'", "<button disabled aria-label='Status'")
                    + "<span>items per page</span><input role='combobox' disabled><input role='combobox' disabled>",
                "/fixture/next" => Grid(5, 30, 2, 2, 25),
                _ => Grid(0, 0, 1, 1, null, statusOption: true),
            };
            if (path == "/fixture/next") nextClicks++;
            await route.FulfillAsync(new() { ContentType = "text/html", Body = html });
        });
        Task transition = CompleteResizeAsync(page);
        IReadOnlyList<PamPendingRequest> result = await new PlaywrightManagedPage(page)
            .ReadAllPendingAsync(null, TestContext.CancellationToken);
        await transition;
        Assert.HasCount(30, result);
        Assert.HasCount(30, result.Select(static r => r.RequestId).Distinct());
        Assert.AreEqual(0, nextClicks);
        // The first 18 IDs represent the baseline; all 12 new IDs must survive the transition.
        Assert.HasCount(12, result.Where(static r => int.Parse(r.RequestId) >= 200018));
    }

    private async Task CompleteResizeAsync(IPage page)
    {
        await page.WaitForURLAsync("**/fixture/large");
        // Controlled server/render latency in the fixture, not a production readiness delay.
        await Task.Delay(900, TestContext.CancellationToken);
        await page.SetContentAsync(Grid(30, 30, 1, 1, 200));
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(60_000)]
    public async Task ReadAllAsync_NoSizeSelector_ReadsBothPagesOf30()
    {
        if (!LocalBrowserEnabled()) return;
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        int nextClicks = 0;
        await page.RouteAsync("**/*", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            if (path == "/fixture/next") nextClicks++;
            await route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body = path switch
                {
                    "/fixture/pending" => Grid(25, 30, 1, 2, null),
                    "/fixture/next" => Grid(5, 30, 2, 2, null),
                    _ => Grid(0, 0, 1, 1, null, statusOption: true),
                },
            });
        });
        IReadOnlyList<PamPendingRequest> result = await new PlaywrightManagedPage(page)
            .ReadAllPendingAsync(null, TestContext.CancellationToken);
        Assert.HasCount(30, result);
        Assert.AreEqual("200029", result[^1].RequestId);
        Assert.AreEqual(1, nextClicks);
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(45_000)]
    public async Task ReadAllAsync_UnknownTotal_DoesNotReturn25AsComplete()
    {
        if (!LocalBrowserEnabled()) return;
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", route => route.FulfillAsync(new()
        {
            ContentType = "text/html",
            Body = new Uri(route.Request.Url).AbsolutePath == "/fixture/pending"
                ? Grid(25, 30, 1, 1, null).Replace("1-25 of 30 items", "total unavailable")
                : Grid(0, 0, 1, 1, null, statusOption: true),
        }));
        PamPendingException failure = await Assert.ThrowsExactlyAsync<PamPendingException>(() =>
            new PlaywrightManagedPage(page).ReadAllPendingAsync(null, TestContext.CancellationToken));
        Assert.AreEqual(PamPendingErrorCode.SnapshotUnstable, failure.ErrorCode);
        Assert.IsNotNull(failure.BrowserDiagnostic);
    }

    private static bool LocalBrowserEnabled() =>
        Environment.GetEnvironmentVariable("PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_GRID") == "LOCAL_ONLY";

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [DataRow("disabled")]
    [DataRow("missing")]
    [DataRow("enabled")]
    [DataRow("no-paginator")]
    [Timeout(30_000)]
    public async Task ReadAllAsync_EmptyPending_DoesNotUsePageSizeControls(string controls)
    {
        if (!LocalBrowserEnabled()) Assert.Inconclusive("Requires explicit local browser opt-in.");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        int unexpectedNavigations = 0;
        await page.RouteAsync("**/*", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            if (path == "/fixture/size-must-not-be-used") unexpectedNavigations++;
            await route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body = path == "/fixture/pending"
                    ? EmptyGrid(controls)
                    : Grid(0, 0, 1, 1, null, statusOption: true),
            });
        });

        IReadOnlyList<PamPendingRequest> result = await new PlaywrightManagedPage(page)
            .ReadAllPendingAsync(null, TestContext.CancellationToken);

        Assert.IsEmpty(result);
        Assert.AreEqual(0, unexpectedNavigations);
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(30_000)]
    public async Task ReadAllAsync_ZeroWhileLoading_WaitsForStableRows()
    {
        if (!LocalBrowserEnabled()) Assert.Inconclusive("Requires explicit local browser opt-in.");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", route => route.FulfillAsync(new()
        {
            ContentType = "text/html",
            Body = new Uri(route.Request.Url).AbsolutePath == "/fixture/pending"
                ? EmptyGrid("disabled").Replace("<button aria-label='Status'", "<button disabled aria-label='Status'")
                : Grid(0, 0, 1, 1, null, statusOption: true),
        }));
        Task loading = CompleteEmptyLoadingAsync(page);
        IReadOnlyList<PamPendingRequest> result;
        try
        {
            result = await new PlaywrightManagedPage(page).ReadAllPendingAsync(null, TestContext.CancellationToken);
        }
        finally
        {
            await loading;
        }

        Assert.HasCount(2, result);
        Assert.AreEqual("200001", result[^1].RequestId);
    }

    private async Task CompleteEmptyLoadingAsync(IPage page)
    {
        await page.WaitForURLAsync("**/fixture/pending");
        await Task.Delay(900, TestContext.CancellationToken);
        await page.SetContentAsync(Grid(2, 2, 1, 1, null));
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30_000)]
    public async Task ReadAllAsync_EmptyViewWithoutConsistentZero_IsNotAccepted(bool conflictingRow)
    {
        if (!LocalBrowserEnabled()) Assert.Inconclusive("Requires explicit local browser opt-in.");
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        string invalidGrid = conflictingRow
            ? Grid(1, 0, 1, 1, null)
            : EmptyGrid("disabled").Replace("0-0 of 0 items", "total unavailable");
        await page.RouteAsync("**/*", route => route.FulfillAsync(new()
        {
            ContentType = "text/html",
            Body = new Uri(route.Request.Url).AbsolutePath == "/fixture/pending"
                ? invalidGrid : Grid(0, 0, 1, 1, null, statusOption: true),
        }));

        PamPendingException error = await Assert.ThrowsExactlyAsync<PamPendingException>(() =>
            new PlaywrightManagedPage(page).ReadAllPendingAsync(null, TestContext.CancellationToken));

        Assert.AreEqual(PamPendingErrorCode.SnapshotUnstable, error.ErrorCode);
    }

    private static string EmptyGrid(string controls)
    {
        string grid = Grid(0, 0, 1, 1, null);
        if (controls == "no-paginator")
            return grid.Replace("<input type='number' aria-label='Page 1/1' value='1'>", string.Empty);
        string selector = controls switch
        {
            "disabled" => "<button role='combobox' disabled>25</button>",
            "enabled" => "<a role='combobox' href='/fixture/size-must-not-be-used'>25</a>",
            _ => string.Empty,
        };
        return grid.Replace("</body>", $"<section><span>items per page</span>{selector}</section></body>");
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(60_000)]
    public async Task ReadAllAsync_MoreThan200Rows_TraversesRemainingPage()
    {
        if (!LocalBrowserEnabled()) return;
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        int nextClicks = 0;
        await page.RouteAsync("**/*", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            if (path == "/fixture/next") nextClicks++;
            await route.FulfillAsync(new()
            {
                ContentType = "text/html",
                Body = path switch
                {
                    "/fixture/pending" => Grid(25, 250, 1, 10, 25, sizeOption: true),
                    "/fixture/large" => Grid(200, 250, 1, 2, 200),
                    "/fixture/next" => Grid(50, 250, 2, 2, 200),
                    _ => Grid(0, 0, 1, 1, null, statusOption: true),
                },
            });
        });
        IReadOnlyList<PamPendingRequest> result = await new PlaywrightManagedPage(page)
            .ReadAllPendingAsync(null, TestContext.CancellationToken);
        Assert.HasCount(250, result);
        Assert.HasCount(250, result.Select(static r => r.RequestId).Distinct());
        Assert.AreEqual(1, nextClicks);
    }

    [TestMethod]
    [TestCategory("ExplicitLocalBrowser")]
    [Timeout(30_000)]
    public async Task ReadAllAsync_DisabledNextWithStalePageCount_FailsPromptlyWithState()
    {
        if (!LocalBrowserEnabled()) return;
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
        IPage page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", route => route.FulfillAsync(new()
        {
            ContentType = "text/html",
            Body = new Uri(route.Request.Url).AbsolutePath == "/fixture/pending"
                ? Grid(25, 30, 1, 2, null).Replace("<a role='button' href='/fixture/next'>Next page</a>", "<button disabled>Next page</button>")
                : Grid(0, 0, 1, 1, null, statusOption: true),
        }));
        long started = Environment.TickCount64;
        PamPendingException failure = await Assert.ThrowsExactlyAsync<PamPendingException>(() =>
            new PlaywrightManagedPage(page).ReadAllPendingAsync(null, TestContext.CancellationToken));
        Assert.AreEqual(PamPendingErrorCode.SnapshotUnstable, failure.ErrorCode);
        Assert.AreEqual("NextPage", failure.BrowserDiagnostic!.Operation);
        Assert.AreEqual(false, failure.BrowserDiagnostic.NextPageEnabled);
        Assert.AreEqual(30, failure.BrowserDiagnostic.TotalItems);
        Assert.IsLessThan(15_000L, Environment.TickCount64 - started);
    }

    private static string Grid(int count, int total, int current, int pages, int? size,
        bool statusOption = false, bool sizeOption = false)
    {
        var html = new StringBuilder("<html><body><button aria-label='Status' title='Pending'>Pending</button>");
        if (statusOption) html.Append("<a role='option' href='/fixture/pending'>Pending</a>");
        if (size is int pageSize)
        {
            html.Append($"<section><span>items per page</span><button role='combobox'>{pageSize}</button></section>");
            if (sizeOption) html.Append("<a role='option' href='/fixture/large'>200</a>");
        }
        int first = total == 0 ? 0 : (current - 1) * (size ?? 25) + 1;
        int last = total == 0 ? 0 : first + count - 1;
        html.Append($"<span>{first}-{last} of {total} items</span><input type='number' aria-label='Page {current}/{pages}' value='{current}'>");
        html.Append(current < pages
            ? "<a role='button' href='/fixture/next'>Next page</a>"
            : "<button disabled>Next page</button>");
        html.Append("<table><tbody>");
        for (int index = 0; index < count; index++)
        {
            html.Append($"<tr aria-label='Row {index + 1}'>")
                .Append(Cell("RequestId", (200000 + first - 1 + index).ToString()))
                .Append(Cell("SystemName", "system"))
                .Append(Cell("Application", "application"))
                .Append(Cell("AccountName", "account"))
                .Append(Cell("ReleaseDt", "Sep 22, 2026, 8:30 AM"))
                .Append(Cell("ExpiresDt", "Sep 22, 2026, 9:30 AM"))
                .Append(Cell("ResponseStatus", "Pending"))
                .Append(Cell("Description", "Fixture"))
                .Append("</tr>");
        }
        return html.Append("</tbody></table></body></html>").ToString();
    }

    private static string Cell(string column, string value) => $"<td data-column-id='{column}'>{value}</td>";
}
