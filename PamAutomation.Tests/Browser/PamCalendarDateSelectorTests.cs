using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Browser;

[TestClass]
[DoNotParallelize]
[TestCategory("ExplicitLocalBrowser")]
public sealed class PamCalendarDateSelectorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("2026-10-01")]
    [DataRow("2026-12-03")]
    [Timeout(25_000)]
    public async Task SelectAsync_MonthDropdownWithPartialInitialGrid_SelectsMonthDirectly(string target)
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.IncludeHeading = true;
        fixture.HidePastDays = true;
        await fixture.ShowAsync();
        Task transition = fixture.ObserveMonthSelectionAsync(Parse(target), TestContext.CancellationToken);
        try { await fixture.SelectAsync(Parse(target), TestContext.CancellationToken); }
        catch
        {
            try { await transition; } catch (PlaywrightException) { }
            throw;
        }
        await transition;
        Assert.AreEqual(1, fixture.MonthSelectionCount);
        Assert.AreEqual(0, fixture.NavigationCount);
        Assert.AreEqual(1, fixture.SelectionCount);
        Assert.AreEqual(0, fixture.SubmissionCount);
    }

    [TestMethod]
    [Timeout(25_000)]
    public async Task SelectAsync_DropdownChangesButGridDoesNot_StopsWithoutClickingDay()
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.IncludeHeading = true;
        await fixture.ShowAsync();
        var failure = await Assert.ThrowsExactlyAsync<PamCalendarSelectionException>(() =>
            fixture.SelectAsync(new(2026, 10, 1), TestContext.CancellationToken));
        Assert.AreEqual(PamCalendarFailure.MonthTransitionTimeout, failure.Failure);
        Assert.AreEqual(0, fixture.NavigationCount);
        Assert.AreEqual(0, fixture.SelectionCount);
    }

    [TestMethod]
    [DataRow("2026-09-01", "2026-10-01", 1)]
    [DataRow("2026-12-01", "2027-01-01", 1)]
    [DataRow("2027-01-01", "2026-12-31", 1)]
    [DataRow("2026-09-01", "2026-09-30", 0)]
    [DataRow("2028-02-01", "2028-02-29", 0)]
    [DataRow("2026-09-01", "2026-12-03", 3)]
    [Timeout(45_000)]
    public async Task SelectAsync_MonthBoundariesAndSlowRedraw_SelectsExactDateWithoutSkipping(string initial, string target, int clicks)
    {
        await using Fixture fixture = await Fixture.CreateAsync(Parse(initial), TestContext.CancellationToken);
        fixture.NavigationDelay = TimeSpan.FromMilliseconds(650);
        fixture.IncludeHiddenCalendar = true;
        await fixture.ShowAsync();

        await fixture.SelectAsync(Parse(target), TestContext.CancellationToken);

        Assert.AreEqual(clicks, fixture.NavigationCount);
        Assert.AreEqual(1, fixture.SelectionCount);
        Assert.AreEqual(0, fixture.SubmissionCount);
        Assert.AreEqual(Parse(target).ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture), await fixture.Input.InputValueAsync());
    }

    [TestMethod]
    [Timeout(25_000)]
    public async Task SelectAsync_AdjacentDuplicateDay_UsesOnlyTargetMonth()
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 10, 1), TestContext.CancellationToken);
        fixture.DuplicateAdjacentDay = true;
        await fixture.ShowAsync();
        await fixture.SelectAsync(new(2026, 10, 1), TestContext.CancellationToken);
        Assert.AreEqual(0, fixture.NavigationCount);
        Assert.AreEqual(1, fixture.SelectionCount);
        Assert.AreEqual(0, fixture.SubmissionCount);
    }

    [TestMethod]
    [Timeout(25_000)]
    public async Task SelectAsync_NoOpNextMonth_StopsAfterOneClickAndReportsObservedMonth()
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.IgnoreNavigation = true;
        await fixture.ShowAsync();
        PamCalendarSelectionException failure = await Assert.ThrowsExactlyAsync<PamCalendarSelectionException>(() =>
            fixture.SelectAsync(new(2026, 10, 1), TestContext.CancellationToken));
        Assert.AreEqual(PamCalendarFailure.MonthTransitionTimeout, failure.Failure);
        Assert.AreEqual(new DateOnly(2026, 9, 1), failure.ObservedMonth);
        Assert.AreEqual(1, failure.NavigationCount);
        Assert.AreEqual(1, fixture.NavigationCount);
        Assert.AreEqual(0, fixture.SelectionCount);
    }

    [TestMethod]
    [DataRow("disabled", PamCalendarFailure.DateUnavailable)]
    [DataRow("duplicate", PamCalendarFailure.AmbiguousCalendar)]
    [DataRow("missing-navigation", PamCalendarFailure.NavigationUnavailable)]
    [DataRow("unreadable", PamCalendarFailure.MonthUnreadable)]
    [DataRow("closed", PamCalendarFailure.OpenFailed)]
    [Timeout(25_000)]
    public async Task SelectAsync_UnsafeCalendar_BlocksWithSpecificReason(string scenario, PamCalendarFailure expected)
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.DisabledDate = scenario == "disabled" ? new DateOnly(2026, 9, 30) : null;
        fixture.DuplicateVisibleCalendar = scenario == "duplicate";
        fixture.MissingNavigation = scenario == "missing-navigation";
        fixture.UnreadableLabels = scenario == "unreadable";
        fixture.ClosedCalendar = scenario == "closed";
        await fixture.ShowAsync();
        DateOnly target = scenario == "missing-navigation" ? new(2026, 10, 1) : new(2026, 9, 30);
        PamCalendarSelectionException failure = await Assert.ThrowsExactlyAsync<PamCalendarSelectionException>(() =>
            fixture.SelectAsync(target, TestContext.CancellationToken));
        Assert.AreEqual(expected, failure.Failure);
        Assert.AreEqual(0, fixture.SelectionCount);
        Assert.AreEqual(0, fixture.NavigationCount);
        Assert.AreEqual(0, fixture.SubmissionCount);
    }

    [TestMethod]
    [Timeout(25_000)]
    public async Task SelectAsync_DayClickDoesNotUpdateInput_DoesNotReportSuccess()
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.IgnoreSelection = true;
        await fixture.ShowAsync();
        PamCalendarSelectionException failure = await Assert.ThrowsExactlyAsync<PamCalendarSelectionException>(() =>
            fixture.SelectAsync(new(2026, 9, 30), TestContext.CancellationToken));
        Assert.AreEqual(PamCalendarFailure.SelectionNotRetained, failure.Failure);
        Assert.AreEqual(1, fixture.SelectionCount);
        Assert.AreEqual(0, fixture.SubmissionCount);
    }

    [TestMethod]
    [Timeout(25_000)]
    public async Task SelectAsync_CalendarOpensAfterFocus_WaitsForVisibleCalendar()
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.ClosedCalendar = true;
        await fixture.ShowAsync();
        Task open = fixture.OpenAfterFocusAsync(TestContext.CancellationToken);
        try { await fixture.SelectAsync(new(2026, 9, 30), TestContext.CancellationToken); }
        finally { await open; }
        Assert.AreEqual(1, fixture.SelectionCount);
        Assert.AreEqual(0, fixture.NavigationCount);
    }

    [TestMethod]
    [Timeout(25_000)]
    public async Task SelectAsync_CancellationDuringTransition_DoesNotNavigateAgain()
    {
        await using Fixture fixture = await Fixture.CreateAsync(new(2026, 9, 1), TestContext.CancellationToken);
        fixture.IgnoreNavigation = true;
        await fixture.ShowAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        Task selecting = fixture.SelectAsync(new(2026, 10, 1), cancellation.Token);
        await fixture.Page.WaitForURLAsync("**/month/2026-10-01");
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => selecting);
        Assert.AreEqual(1, fixture.NavigationCount);
        Assert.AreEqual(0, fixture.SelectionCount);
    }

    private static DateOnly Parse(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // HTML models the external portal only. All requests are intercepted in C#; no JS, PAM or credentials.
    private sealed class Fixture(IPlaywright playwright, IBrowser browser, IPage page, DateOnly month) : IAsyncDisposable
    {
        public IPage Page { get; } = page;
        public ILocator Input => Page.GetByLabel("Start Date", new() { Exact = true });
        private DateOnly month = month;
        private Task transition = Task.CompletedTask;
        private string selected = string.Empty;
        public TimeSpan NavigationDelay { get; set; }
        public bool IgnoreNavigation { get; set; }
        public bool IgnoreSelection { get; set; }
        public bool IncludeHiddenCalendar { get; set; }
        public bool DuplicateAdjacentDay { get; set; }
        public bool DuplicateVisibleCalendar { get; set; }
        public bool MissingNavigation { get; set; }
        public bool UnreadableLabels { get; set; }
        public bool ClosedCalendar { get; set; }
        public bool IncludeHeading { get; set; }
        public bool HidePastDays { get; set; }
        public int MonthSelectionCount { get; private set; }
        public DateOnly? DisabledDate { get; set; }
        public int NavigationCount { get; private set; }
        public int SelectionCount { get; private set; }
        public int SubmissionCount { get; private set; }

        public static async Task<Fixture> CreateAsync(DateOnly month, CancellationToken token)
        {
            if (Environment.GetEnvironmentVariable("PAM_AUTOMATION_ALLOW_LOCAL_BROWSER_CALENDAR") != "LOCAL_ONLY")
                Assert.Inconclusive("Requires local-only visible Edge opt-in; never contacts PAM.");
            token.ThrowIfCancellationRequested();
            IPlaywright playwright = await Playwright.CreateAsync();
            try
            {
                IBrowser browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = false });
                try
                {
                    IPage page = await browser.NewPageAsync();
                    page.SetDefaultTimeout(5_000);
                    var fixture = new Fixture(playwright, browser, page, month);
                    await page.RouteAsync("**/*", fixture.HandleAsync);
                    return fixture;
                }
                catch { await browser.DisposeAsync(); throw; }
            }
            catch { playwright.Dispose(); throw; }
        }

        public Task ShowAsync() => Page.GotoAsync("https://calendar.invalid/fixture");
        public Task SelectAsync(DateOnly target, CancellationToken token) => new PamCalendarDateSelector(Page).SelectAsync(Input, target, token);

        public async Task ObserveMonthSelectionAsync(DateOnly target, CancellationToken token)
        {
            await Assertions.Expect(Page.Locator("select.flatpickr-monthDropdown-months"))
                .ToHaveValueAsync((target.Month - 1).ToString(CultureInfo.InvariantCulture));
            MonthSelectionCount++;
            await Task.Delay(650, token);
            month = new DateOnly(target.Year, target.Month, 1);
            await Page.SetContentAsync(Html(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        public async Task OpenAfterFocusAsync(CancellationToken token)
        {
            await Assertions.Expect(Input).ToBeFocusedAsync();
            await Task.Delay(400, token);
            ClosedCalendar = false;
            await Page.SetContentAsync(Html(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        private async Task HandleAsync(IRoute route)
        {
            var uri = new Uri(route.Request.Url);
            if (uri.Host != "calendar.invalid" || route.Request.Method != "GET")
            {
                SubmissionCount++;
                await route.AbortAsync();
                return;
            }
            string path = uri.AbsolutePath;
            DateOnly? next = null;
            if (path.StartsWith("/month/", StringComparison.Ordinal))
            {
                NavigationCount++;
                if (!IgnoreNavigation) next = Parse(path[7..]);
                if (NavigationDelay == TimeSpan.Zero && next.HasValue) month = next.Value;
            }
            else if (path.StartsWith("/choose/", StringComparison.Ordinal))
            {
                SelectionCount++;
                if (!IgnoreSelection) selected = Parse(path[8..]).ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
            }
            else if (path != "/fixture")
            {
                SubmissionCount++;
                await route.AbortAsync();
                return;
            }
            await route.FulfillAsync(new() { ContentType = "text/html", Body = Html() });
            if (next.HasValue && NavigationDelay > TimeSpan.Zero) transition = RedrawAsync(next.Value);
        }

        private async Task RedrawAsync(DateOnly next)
        {
            await Task.Delay(NavigationDelay);
            month = next;
            await Page.SetContentAsync(Html(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        private string Html()
        {
            var days = new StringBuilder();
            days.Append(Day(month.AddDays(-1), "prevMonthDay"));
            for (int day = 1; day <= DateTime.DaysInMonth(month.Year, month.Month); day++)
            {
                if (HidePastDays && month == new DateOnly(2026, 9, 1) && day < 30) continue;
                days.Append(Day(new DateOnly(month.Year, month.Month, day), string.Empty));
            }
            days.Append(Day(month.AddMonths(1), "nextMonthDay"));
            if (DuplicateAdjacentDay) days.Append(Day(month, "nextMonthDay"));
            string calendar = $"<section class='flatpickr-calendar open' style='{(ClosedCalendar ? "display:none" : "")}'><h2>{month:MMMM yyyy}</h2>" +
                (IncludeHeading ? "<select class='flatpickr-monthDropdown-months'>" +
                    string.Concat(Enumerable.Range(0, 12).Select(value => $"<option value='{value}' {(value == month.Month - 1 ? "selected" : "")}>{value + 1}</option>")) +
                    $"</select><input class='cur-year' value='{month.Year}' readonly>" : string.Empty) +
                (MissingNavigation ? string.Empty : $"<a class='flatpickr-prev-month' href='/month/{month.AddMonths(-1):yyyy-MM-dd}'>Anterior</a> " +
                    $"<a class='flatpickr-next-month' href='/month/{month.AddMonths(1):yyyy-MM-dd}'>Siguiente</a>") +
                $"<div class='days'>{days}</div></section>";
            return "<!doctype html><html><head><title>Prueba local de calendario PAM</title>" +
                "<style>body{font:16px sans-serif;margin:30px}input{width:230px;padding:8px}.days{display:grid;grid-template-columns:repeat(7,48px);gap:4px}.flatpickr-day{padding:12px 4px}.flatpickr-disabled{color:gray}</style>" +
                $"</head><body><h1>Calendario simulado; sin solicitudes PAM</h1><label>Start Date<input readonly value='{WebUtility.HtmlEncode(selected)}'></label>" +
                calendar + (IncludeHiddenCalendar ? "<div style='display:none'>" + calendar + "</div>" : string.Empty) +
                (DuplicateVisibleCalendar ? calendar : string.Empty) + "</body></html>";
        }

        private string Day(DateOnly date, string css) => $"<a class='flatpickr-day {css} {(DisabledDate == date ? "flatpickr-disabled" : "")}' " +
            $"aria-label='{(UnreadableLabels ? "Unexpected" : date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture))}' href='/choose/{date:yyyy-MM-dd}'>{date.Day}</a>";

        public async ValueTask DisposeAsync()
        {
            try { await transition; }
            finally { await browser.DisposeAsync(); playwright.Dispose(); }
        }
    }
}
