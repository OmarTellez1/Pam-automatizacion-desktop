using System.Diagnostics;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Browser;

[TestClass]
[DoNotParallelize]
[TestCategory("ExplicitLocalBrowser")]
public sealed class PamRequestFormStabilityTests
{
    public TestContext TestContext { get; set; }
    private static readonly PamRequestFormData Form = new(new(2026, 11, 2), new(9, 0), 0, 1, 0, "fixture reason");

    [TestMethod]
    [DataRow("late-reset")]
    [DataRow("replacement")]
    [DataRow("same-length-reason")]
    [DataRow("partial-read")]
    [Timeout(30_000)]
    public async Task Verification_TransientChangeRequiresNewStableWindowAndOneCorrection(string scenario)
    {
        await using Fixture fixture = await Fixture.CreateAsync(TestContext.CancellationToken);
        var timer = Stopwatch.StartNew();
        Task validation = fixture.Managed.VerifyRequestFormAsync(Form, PamRequestFormVerificationStage.BeforeSubmit, TestContext.CancellationToken);
        await Task.Delay(350, TestContext.CancellationToken);
        if (scenario == "same-length-reason") await fixture.Page.GetByLabel("Reason", new() { Exact = true }).FillAsync("wrongxx reason");
        else if (scenario == "replacement") await fixture.Page.SetContentAsync(Html());
        else if (scenario == "partial-read")
        {
            await fixture.Page.SetContentAsync(Html().Replace("id='time'", "id='missing'"));
            await Task.Delay(250, TestContext.CancellationToken);
            await fixture.Page.SetContentAsync(Html());
        }
        else await fixture.Page.GetByLabel("Start Time", new() { Exact = true }).FillAsync("08:00");
        await validation;
        Assert.IsGreaterThanOrEqualTo(900L, timer.ElapsedMilliseconds);
        Assert.AreEqual("09:00", await fixture.Page.GetByLabel("Start Time", new() { Exact = true }).InputValueAsync());
        Assert.AreEqual(Form.Reason, await fixture.Page.GetByLabel("Reason", new() { Exact = true }).InputValueAsync());
        Assert.AreEqual(0, fixture.Posts);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task Verification_SecondResetAfterCorrection_BlocksAndReportsExhaustedCorrection()
    {
        await using Fixture fixture = await Fixture.CreateAsync(TestContext.CancellationToken);
        ILocator time = fixture.Page.GetByLabel("Start Time", new() { Exact = true });
        await time.FillAsync("08:00");
        Task validation = fixture.Managed.VerifyRequestFormAsync(Form, PamRequestFormVerificationStage.AfterFill, TestContext.CancellationToken);
        await Assertions.Expect(time).ToHaveValueAsync("09:00");
        await time.FillAsync("08:00");
        var failure = await Assert.ThrowsExactlyAsync<PamRequestFormVerificationException>(() => validation);
        Assert.AreEqual(1, failure.CorrectionCount);
        Assert.AreEqual(PamRequestFormMismatchField.StartTime, failure.CorrectedFields);
        Assert.IsTrue(failure.MismatchFields.HasFlag(PamRequestFormMismatchField.StartTime));
        Assert.AreEqual(0, fixture.Posts);
    }

    [TestMethod]
    public async Task Verification_Canceled_DoesNotSubmit()
    {
        await using Fixture fixture = await Fixture.CreateAsync(TestContext.CancellationToken);
        using var cancel = new CancellationTokenSource(250);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Managed.VerifyRequestFormAsync(Form, PamRequestFormVerificationStage.BeforeSubmit, cancel.Token));
        Assert.AreEqual(0, fixture.Posts);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task Verification_DateCorrectionResetsHour_RepairsDependentHourAndVerifiesAllFields()
    {
        await using Fixture fixture = await Fixture.CreateAsync(TestContext.CancellationToken);
        await fixture.Page.UnrouteAsync("**/*");
        await fixture.Page.RouteAsync("**/*", async route =>
        {
            if (route.Request.Method == "GET" && route.Request.Url == "https://form.invalid/choose")
                await route.FulfillAsync(new() { ContentType = "text/html", Body = Html().Replace("value='09:00'", "value='08:00'") });
            else await route.AbortAsync();
        });
        string calendar = "<section class='flatpickr-calendar open'><select class='flatpickr-monthDropdown-months'><option selected value='10'>November</option></select><input class='cur-year' value='2026'><a class='flatpickr-day' aria-label='November 2, 2026' href='https://form.invalid/choose'>2</a></section>";
        await fixture.Page.SetContentAsync(Html().Replace("November 02, 2026", "November 01, 2026").Replace("</body>", calendar + "</body>"));
        await fixture.Managed.VerifyRequestFormAsync(Form, PamRequestFormVerificationStage.BeforeSubmit, TestContext.CancellationToken);
        Assert.AreEqual("November 02, 2026", await fixture.Page.GetByLabel("Start Date", new() { Exact = true }).InputValueAsync());
        Assert.AreEqual("09:00", await fixture.Page.GetByLabel("Start Time", new() { Exact = true }).InputValueAsync());
        Assert.AreEqual(0, fixture.Posts);
    }

    [TestMethod]
    public async Task Fill_WaitsForEditableControlThenVerifiesWithoutSubmitting()
    {
        await using Fixture fixture = await Fixture.CreateAsync(TestContext.CancellationToken);
        await fixture.Page.SetContentAsync(Html().Replace("id='time'", "id='time' readonly"));
        Task filling = fixture.Managed.FillAndVerifyRequestFormAsync(Form, TestContext.CancellationToken);
        await Task.Delay(350, TestContext.CancellationToken);
        await fixture.Page.SetContentAsync(Html());
        await filling;
        Assert.AreEqual("09:00", await fixture.Page.GetByLabel("Start Time", new() { Exact = true }).InputValueAsync());
        Assert.AreEqual(0, fixture.Posts);
    }


    private static string Html() => """
        <html><body>
        <label for='date'>Start Date</label><input id='date' readonly value='November 02, 2026'>
        <label for='time'>Start Time</label><input id='time' value='09:00'>
        <label for='days'>Days</label><input id='days' value='0'>
        <label for='hours'>Hours</label><input id='hours' value='1'>
        <label for='minutes'>Minutes</label><input id='minutes' value='0'>
        <label for='reason'>Reason</label><textarea id='reason'>fixture reason</textarea>
        <button role='switch' aria-label='Application Session' aria-checked='true'>Application Session</button>
        </body></html>
        """;

    private sealed class Fixture(IPlaywright playwright, IBrowser browser, IPage page) : IAsyncDisposable
    {
        public IPage Page { get; } = page;
        public PlaywrightManagedPage Managed { get; } = new(page);
        public int Posts;
        public static async Task<Fixture> CreateAsync(CancellationToken token)
        {
            if (Environment.GetEnvironmentVariable("PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_CALENDAR") != "LOCAL_ONLY")
                Assert.Inconclusive("Requires local browser opt-in.");
            IPlaywright playwright = await Playwright.CreateAsync();
            IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
            IPage page = await browser.NewPageAsync();
            var fixture = new Fixture(playwright, browser, page);
            page.Request += (_, request) => { if (request.Method == "POST") Interlocked.Increment(ref fixture.Posts); };
            await page.RouteAsync("**/*", route => route.AbortAsync());
            await page.SetContentAsync(Html()).WaitAsync(token);
            return fixture;
        }
        public async ValueTask DisposeAsync() { await browser.CloseAsync(); playwright.Dispose(); }
    }
}
