using System.Globalization;
using Microsoft.Playwright;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Resilience;
using PamAutomation.Core.Diagnostics;

namespace PamAutomation.Browser;

/// <summary>Selects a date through the visible calendar, observing each transition before acting again.</summary>
internal sealed class PamCalendarDateSelector(IPage page)
{
    private const int MaximumMonthChanges = 120;
    private const string CurrentDays = ".flatpickr-day:not(.prevMonthDay):not(.nextMonthDay):not(.hidden):visible";
    private static TimeSpan StepTimeout => PamOperationTimingPolicy.CalendarTransitionTimeout;
    private static TimeSpan TotalTimeout => PamOperationTimingPolicy.CalendarSelectionTimeout;
    private static float ProbeTimeout => (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds;
    private readonly IPage page = page ?? throw new ArgumentNullException(nameof(page));
    private DateOnly targetDate;
    private DateOnly? observedMonth;
    private int navigationCount;
    private readonly Queue<PamCalendarObservation> history = new();
    private long selectionStarted;
    private int observationCount;
    private int readFailureCount;
    private int consecutiveMatches;
    private PamCalendarStage stage;

    public async Task SelectAsync(ILocator startDate, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startDate);
        targetDate = date;
        PamExecutionAttempt.Current?.RecordTargetDate(date);
        PamExecutionAttempt.Current?.RecordStage("Calendar");
        observedMonth = null;
        navigationCount = 0;
        history.Clear();
        selectionStarted = Environment.TickCount64;
        observationCount = readFailureCount = consecutiveMatches = 0;
        stage = PamCalendarStage.ConfirmInitialDate;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TotalTimeout);
        CancellationToken token = budget.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            // The already-selected path must also retain its value across observations.
            if (await HasSelectedDateAsync(startDate, token).ConfigureAwait(false))
            {
                await Task.Delay(PamOperationTimingPolicy.ObservationInterval, token).ConfigureAwait(false);
                if (await HasSelectedDateAsync(startDate, token).ConfigureAwait(false)) return;
                Observe(PamCalendarReadCondition.SelectionReverted);
            }
            ILocator calendar = page.Locator(PamRequestTestSelectors.OpenCalendar + ":visible");
            stage = PamCalendarStage.Open;
            await OpenAsync(calendar, startDate, token).ConfigureAwait(false);
            stage = PamCalendarStage.ReadInitialMonth;
            DateOnly month = await WaitForMonthAsync(calendar, null, token).ConfigureAwait(false);
            DateOnly targetMonth = new(date.Year, date.Month, 1);
            int distance = (date.Year - month.Year) * 12 + date.Month - month.Month;
            if (Math.Abs(distance) > MaximumMonthChanges) throw Failure(PamCalendarFailure.NavigationLimit);

            while (month != targetMonth)
            {
                stage = PamCalendarStage.SelectMonth;
                // Flatpickr exposes a native month selector. Prefer the requested month directly
                // within the displayed year; arrows are only needed to cross a year boundary.
                ILocator monthSelector = calendar.Locator("select.flatpickr-monthDropdown-months:visible");
                string targetMonthValue = (date.Month - 1).ToString(CultureInfo.InvariantCulture);
                if (month.Year == date.Year && await monthSelector.CountAsync().WaitAsync(token).ConfigureAwait(false) == 1)
                {
                    ILocator option = monthSelector.Locator($"option[value=\"{targetMonthValue}\"]");
                    if (await option.CountAsync().WaitAsync(token).ConfigureAwait(false) != 1 ||
                        !await option.IsEnabledAsync(new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false))
                        throw Failure(PamCalendarFailure.DateUnavailable);
                    navigationCount++;
                    await monthSelector.SelectOptionAsync(new SelectOptionValue { Value = targetMonthValue }, new() { Timeout = (float)StepTimeout.TotalMilliseconds })
                        .WaitAsync(token).ConfigureAwait(false);
                    stage = PamCalendarStage.WaitTransition;
                    month = await WaitForMonthAsync(calendar, targetMonth, token).ConfigureAwait(false);
                    continue;
                }
                int direction = month < targetMonth ? 1 : -1;
                ILocator control = calendar.Locator(direction > 0
                    ? PamRequestTestSelectors.NextMonth : PamRequestTestSelectors.PreviousMonth).Filter(new() { Visible = true });
                if (await control.CountAsync().WaitAsync(token).ConfigureAwait(false) != 1 ||
                    !await control.IsEnabledAsync(new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false) ||
                    await control.GetAttributeAsync("aria-disabled", new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false) == "true")
                    throw Failure(PamCalendarFailure.NavigationUnavailable);
                navigationCount++;
                await control.ClickAsync(new() { Timeout = (float)StepTimeout.TotalMilliseconds }).WaitAsync(token).ConfigureAwait(false);
                // A delayed/no-op click must never cause another click past the requested month.
                stage = PamCalendarStage.WaitTransition;
                month = await WaitForMonthAsync(calendar, month.AddMonths(direction), token).ConfigureAwait(false);
            }

            stage = PamCalendarStage.SelectDay;
            string label = date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            ILocator day = calendar.Locator(CurrentDays + $"[aria-label=\"{label}\"]");
            int count = await day.CountAsync().WaitAsync(token).ConfigureAwait(false);
            if (count > 1) throw Failure(PamCalendarFailure.AmbiguousDate);
            if (count != 1)
            {
                Observe(PamCalendarReadCondition.TargetDayMissing, days: count);
                throw Failure(PamCalendarFailure.DateUnavailable);
            }
            if (
                (await day.GetAttributeAsync("class", new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false))?
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("flatpickr-disabled") == true ||
                await day.GetAttributeAsync("aria-disabled", new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false) == "true" ||
                !await day.IsEnabledAsync(new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false))
            {
                Observe(PamCalendarReadCondition.TargetDayDisabled, days: count);
                throw Failure(PamCalendarFailure.DateUnavailable);
            }

            await day.ClickAsync(new() { Timeout = (float)StepTimeout.TotalMilliseconds }).WaitAsync(token).ConfigureAwait(false);
            // Validate the input, not just the click or the calendar closing. Never click Submit here.
            long started = Environment.TickCount64;
            stage = PamCalendarStage.ConfirmDate;
            int matchingSamples = 0;
            bool everMatched = false;
            while (Environment.TickCount64 - started < StepTimeout.TotalMilliseconds)
            {
                matchingSamples = await HasSelectedDateAsync(startDate, token).ConfigureAwait(false) ? matchingSamples + 1 : 0;
                Observe(matchingSamples > 0 ? PamCalendarReadCondition.Valid : everMatched
                    ? PamCalendarReadCondition.SelectionReverted : PamCalendarReadCondition.SelectionMismatch);
                everMatched |= matchingSamples > 0;
                consecutiveMatches = matchingSamples;
                if (matchingSamples >= 2) return;
                await Task.Delay(PamOperationTimingPolicy.ObservationInterval, token).ConfigureAwait(false);
            }
            throw Failure(PamCalendarFailure.SelectionNotRetained);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Failure(PamCalendarFailure.Timeout);
        }
        catch (PlaywrightException exception)
        {
            throw Failure(PamCalendarFailure.PortalReadFailed, exception);
        }
        catch (TimeoutException exception)
        {
            throw Failure(PamCalendarFailure.Timeout, exception);
        }
    }

    private async Task OpenAsync(ILocator calendar, ILocator startDate, CancellationToken token)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (await OpenCountAsync(calendar, token).ConfigureAwait(false) == 1) return;
            await startDate.ClickAsync(new() { Timeout = (float)StepTimeout.TotalMilliseconds }).WaitAsync(token).ConfigureAwait(false);
            long started = Environment.TickCount64;
            while (Environment.TickCount64 - started < 1_000)
            {
                if (await OpenCountAsync(calendar, token).ConfigureAwait(false) == 1) return;
                await Task.Delay(PamOperationTimingPolicy.ObservationInterval, token).ConfigureAwait(false);
            }
        }
        throw Failure(PamCalendarFailure.OpenFailed);
    }

    private async Task<int> OpenCountAsync(ILocator calendar, CancellationToken token)
    {
        int count = await calendar.CountAsync().WaitAsync(token).ConfigureAwait(false);
        if (count != 1) Observe(count > 1 ? PamCalendarReadCondition.MultipleVisibleCalendars
            : PamCalendarReadCondition.CalendarNotVisible, count);
        if (count > 1) throw Failure(PamCalendarFailure.AmbiguousCalendar);
        return count;
    }

    private async Task<DateOnly> WaitForMonthAsync(ILocator calendar, DateOnly? expected, CancellationToken token)
    {
        long started = Environment.TickCount64;
        DateOnly? previous = null;
        while (Environment.TickCount64 - started < StepTimeout.TotalMilliseconds)
        {
            token.ThrowIfCancellationRequested();
            DateOnly? current = null;
            try
            {
                if (await OpenCountAsync(calendar, token).ConfigureAwait(false) == 1)
                    current = await ReadMonthAsync(calendar, expected, token).ConfigureAwait(false);
            }
            catch (PlaywrightException) { readFailureCount++; Observe(PamCalendarReadCondition.CalendarReadFailed); }
            catch (TimeoutException) { readFailureCount++; Observe(PamCalendarReadCondition.CalendarReadTimeout); }
            if (current.HasValue)
            {
                observedMonth = current;
                consecutiveMatches = expected is null || current == expected
                    ? current == previous ? consecutiveMatches + 1 : 1 : 0;
                if (consecutiveMatches >= 2 && current == previous) return current.Value;
            }
            else consecutiveMatches = 0;
            previous = current;
            await Task.Delay(PamOperationTimingPolicy.ObservationInterval, token).ConfigureAwait(false);
        }
        throw Failure(expected is null ? PamCalendarFailure.MonthUnreadable : PamCalendarFailure.MonthTransitionTimeout);
    }

    private async Task<DateOnly?> ReadMonthAsync(ILocator calendar, DateOnly? expected, CancellationToken token)
    {
        ILocator monthSelector = calendar.Locator("select.flatpickr-monthDropdown-months:visible");
        ILocator yearInput = calendar.Locator("input.cur-year:visible");
        int monthCount = await monthSelector.CountAsync().WaitAsync(token).ConfigureAwait(false);
        int yearCount = await yearInput.CountAsync().WaitAsync(token).ConfigureAwait(false);
        if (monthCount != 0 || yearCount != 0)
        {
            if (monthCount != 1 || yearCount != 1)
            {
                Observe(monthCount > 1 || yearCount > 1 ? PamCalendarReadCondition.HeaderAmbiguous
                    : PamCalendarReadCondition.HeaderIncomplete, 1, monthCount, yearCount, native: true);
                return null;
            }
            if (!int.TryParse(await monthSelector.InputValueAsync(new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false),
                    NumberStyles.None, CultureInfo.InvariantCulture, out int month) || month is < 0 or > 11)
            {
                Observe(PamCalendarReadCondition.MonthValueInvalid, 1, monthCount, yearCount, native: true);
                return null;
            }
            if (!int.TryParse(await yearInput.InputValueAsync(new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false),
                    NumberStyles.None, CultureInfo.InvariantCulture, out int year) || year is < 1 or > 9999)
            {
                Observe(PamCalendarReadCondition.YearValueInvalid, 1, monthCount, yearCount, native: true);
                return null;
            }
            DateOnly header = new(year, month + 1, 1);
            // The heading is authoritative; a day from its grid must agree before accepting it.
            // Disabled/clipped days need not form a complete visible month.
            ILocator currentDays = calendar.Locator(CurrentDays);
            int dayCount = await currentDays.CountAsync().WaitAsync(token).ConfigureAwait(false);
            if (dayCount == 0)
            {
                Observe(PamCalendarReadCondition.GridEmpty, 1, monthCount, yearCount, dayCount, header, native: true);
                return null;
            }
            string? firstLabel = await currentDays.First.GetAttributeAsync("aria-label", new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false);
            if (!DateOnly.TryParseExact(firstLabel, "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly firstDay))
            {
                Observe(PamCalendarReadCondition.DayLabelInvalid, 1, monthCount, yearCount, dayCount, header, native: true);
                return null;
            }
            DateOnly grid = new(firstDay.Year, firstDay.Month, 1);
            Observe(header != grid ? PamCalendarReadCondition.HeaderGridMismatch :
                expected is not null && header != expected ? PamCalendarReadCondition.UnexpectedMonth : PamCalendarReadCondition.Valid,
                1, monthCount, yearCount, dayCount, header, grid, true);
            return header == grid ? header : null;
        }

        // Compatibility for a calendar without a native heading: require a complete grid.
        ILocator days = calendar.Locator(CurrentDays);
        int count = await days.CountAsync().WaitAsync(token).ConfigureAwait(false);
        if (count is < 28 or > 31)
        {
            Observe(count == 0 ? PamCalendarReadCondition.GridEmpty : PamCalendarReadCondition.FallbackGridIncomplete,
                1, 0, 0, days: count);
            return null;
        }
        string? first = await days.First.GetAttributeAsync("aria-label", new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false);
        string? last = await days.Last.GetAttributeAsync("aria-label", new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false);
        if (!DateOnly.TryParseExact(first, "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly begin) ||
            !DateOnly.TryParseExact(last, "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly end))
        {
            Observe(PamCalendarReadCondition.DayLabelInvalid, 1, 0, 0, days: count);
            return null;
        }
        if (begin.Day != 1 || begin.Year != end.Year || begin.Month != end.Month ||
            end.Day != DateTime.DaysInMonth(begin.Year, begin.Month) || count != end.Day)
        {
            Observe(PamCalendarReadCondition.FallbackGridIncomplete, 1, 0, 0, days: count,
                grid: new(begin.Year, begin.Month, 1));
            return null;
        }
        Observe(expected is not null && begin != expected ? PamCalendarReadCondition.UnexpectedMonth
            : PamCalendarReadCondition.Valid, 1, 0, 0, days: count, grid: begin);
        return begin;
    }

    private async Task<bool> HasSelectedDateAsync(ILocator startDate, CancellationToken token) =>
        string.Equals(await startDate.InputValueAsync(new() { Timeout = ProbeTimeout }).WaitAsync(token).ConfigureAwait(false),
            targetDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private void Observe(PamCalendarReadCondition condition, int? calendars = null, int? months = null,
        int? years = null, int? days = null, DateOnly? header = null, DateOnly? grid = null, bool native = false)
    {
        observationCount++;
        long elapsed = Environment.TickCount64 - selectionStarted;
        var observation = new PamCalendarObservation(stage, condition, elapsed,
            Math.Max(0, (long)TotalTimeout.TotalMilliseconds - elapsed), calendars, months, years,
            days, header, grid, native);
        // Keep transitions, not an event for every poll. Counters still include all observations.
        if (history.LastOrDefault() is { } previous && (previous with
        { ElapsedMilliseconds = elapsed, RemainingMilliseconds = observation.RemainingMilliseconds }) == observation) return;
        history.Enqueue(observation);
        while (history.Count > 20) history.Dequeue();
    }

    private PamCalendarSelectionException Failure(PamCalendarFailure reason, Exception? inner = null)
    {
        DiagnosticAttemptSummary? attempt = PamExecutionAttempt.Current?.Capture();
        return new(reason, targetDate, observedMonth, navigationCount, inner,
            new PamCalendarDiagnostic(attempt?.AttemptId, attempt?.FormOrdinal ?? 0,
                attempt?.ExpectedCount ?? 0, observationCount, readFailureCount, consecutiveMatches,
                Array.AsReadOnly(history.ToArray())));
    }
}
