using Microsoft.Playwright;
using PamAutomation.Core.Resilience;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private const int MaximumPendingPages = 500;
    private const string PendingCaptureName = "fase13-pending-grid.png";
    internal const string PendingGridSnapshotExpression = """
        rows => rows.map(row => ({
            cells: Array.from(row.querySelectorAll('td[data-column-id]')).map(cell => ({
                columnId: cell.getAttribute('data-column-id') ?? '',
                value: (cell.textContent ?? '').replace(/\s+/g, ' ').trim()
            }))
        }))
        """;

    private PamAutomation.Core.Diagnostics.PendingBrowserReadDiagnostic pendingReadDiagnostic = new("NotStarted");
    private static float PendingProbeMilliseconds => (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds;
    private static float PendingControlMilliseconds => (float)PamOperationTimingPolicy.PendingControlTimeout.TotalMilliseconds;

    public async Task<IReadOnlyList<PamPendingRequest>> ReadAllPendingAsync(
        string? captureDirectory,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(PamOperationTimingPolicy.PendingReadTimeout);
        pendingReadDiagnostic = new("NavigatePending");
        try
        {
            await page.GotoAsync(PamPendingSelectors.RequestsUri.AbsoluteUri, new()
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = (float)PamOperationTimingPolicy.PendingReadTimeout.TotalMilliseconds,
            }).WaitAsync(deadline.Token).ConfigureAwait(false);
            pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "SelectPendingStatus" };
            await SelectPendingStatusAsync(deadline.Token).ConfigureAwait(false);
            pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "SelectPageSize" };
            int? pageSize = await SelectLargestPendingPageSizeAsync(deadline.Token).ConfigureAwait(false);

            ILocator rows = page.Locator(PamPendingSelectors.Rows);
            ILocator pageInput = page.Locator(PamPendingSelectors.PageInput);
            for (int scan = 1; scan <= PamOperationTimingPolicy.GridScanMaximumAttempts; scan++)
            {
                try
                {
                    IReadOnlyList<PamPendingRequest> result = await ScanAllPendingPagesAsync(
                        rows, pageInput, pageSize, deadline.Token).ConfigureAwait(false);
                    await CapturePendingViewsAsync(captureDirectory, deadline.Token).ConfigureAwait(false);
                    return result;
                }
                catch (PamPendingException exception) when (
                    exception.ErrorCode == PamPendingErrorCode.SnapshotUnstable &&
                    scan < PamOperationTimingPolicy.GridScanMaximumAttempts)
                {
                    // Re-observe the live pagination, never click using a cached page count.
                    await Task.Delay(PamOperationTimingPolicy.GridScanRetryDelay, deadline.Token).ConfigureAwait(false);
                }
            }
            throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            // WaitAsync cancels the caller, not the Playwright command. Closing this owned page
            // prevents a delayed click from acting on a subsequent recovery navigation.
            try { await page.CloseAsync().ConfigureAwait(false); }
            catch (PlaywrightException) { /* The session owner still disposes the context/browser. */ }
            throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable)
            {
                BrowserDiagnostic = pendingReadDiagnostic,
                RequiresNewSession = true,
            };
        }
        catch (TimeoutException exception)
        {
            throw new PamPendingException(PamPendingErrorCode.PortalUnavailable, exception)
            {
                BrowserDiagnostic = pendingReadDiagnostic,
            };
        }
        catch (PamPendingException exception)
        {
            throw new PamPendingException(exception.ErrorCode, exception)
            {
                BrowserDiagnostic = pendingReadDiagnostic,
                RequiresNewSession = exception.RequiresNewSession,
            };
        }
        catch (Exception exception) when (exception is PlaywrightException or InvalidOperationException or ArgumentException)
        {
            throw new PamPendingException(PamPendingErrorCode.InvalidRequest, exception)
            {
                BrowserDiagnostic = pendingReadDiagnostic,
            };
        }
    }

    private async Task CapturePendingViewsAsync(string? captureDirectory, CancellationToken cancellationToken)
    {
        await CaptureAsync(captureDirectory, PendingCaptureName, cancellationToken).ConfigureAwait(false);
        await CapturePendingColumnViewAsync(captureDirectory, PamPendingSelectors.DescriptionCell,
            "fase13-pending-grid-description-and-expiration.png", cancellationToken).ConfigureAwait(false);
        await CapturePendingColumnViewAsync(captureDirectory, PamPendingSelectors.ApplicationCell,
            "fase13-pending-grid-application.png", cancellationToken).ConfigureAwait(false);
    }

    private async Task<int?> SelectLargestPendingPageSizeAsync(CancellationToken cancellationToken)
    {
        // Wait for the filtered table before inspecting controls; they can be replaced during load.
        ILocator pageInput = page.Locator(PamPendingSelectors.PageInput);
        ILocator rows = page.Locator(PamPendingSelectors.Rows);
        PendingView view = await ReadStablePendingViewAsync(rows, pageInput, null, cancellationToken).ConfigureAwait(false);
        // Empty Pending can retain a disabled or incomplete page-size control. A stable zero
        // needs no resize; the normal scan below still revalidates the rows and total.
        if (view.Position.Items.Total == 0) return null;
        ILocator labels = page.GetByText(PamPendingSelectors.ItemsPerPageExpression());
        ILocator? visibleLabel = null;
        int labelCount = await labels.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < labelCount; index++)
        {
            ILocator candidate = labels.Nth(index);
            if (!await candidate.IsVisibleAsync().WaitAsync(cancellationToken).ConfigureAwait(false)) continue;
            if (visibleLabel is not null)
                throw new PamPendingException(PamPendingErrorCode.InvalidRequest);
            visibleLabel = candidate;
        }
        if (visibleLabel is null) return null;

        ILocator selector = visibleLabel.Locator("xpath=ancestor::*[.//*[@role='combobox']][1]")
            .GetByRole(AriaRole.Combobox);
        await selector.ClickAsync(new() { Timeout = PendingControlMilliseconds })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        ILocator options = page.GetByRole(AriaRole.Option);
        ILocator? largestOption = null;
        int largest = 0;
        int optionCount = await options.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < optionCount; index++)
        {
            ILocator option = options.Nth(index);
            if (!await option.IsVisibleAsync().WaitAsync(cancellationToken).ConfigureAwait(false)) continue;
            string text = (await option.InnerTextAsync(new() { Timeout = PendingProbeMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false)).Trim();
            if (int.TryParse(text, out int size) && size > largest)
            {
                largestOption = option;
                largest = size;
            }
        }
        if (largestOption is null) throw new PamPendingException(PamPendingErrorCode.InvalidRequest);
        await largestOption.ClickAsync(new() { Timeout = PendingControlMilliseconds })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "ConfirmPageSize" };
        // PAM removes the size selector while loading. Re-resolving its ancestor then can
        // target the Status/Filter controls instead. Confirm completion from the resulting grid.
        bool confirmed = await WaitUntilAsync(async () =>
        {
            return !await largestOption.IsVisibleAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        if (!confirmed) throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
        await ReadStablePendingViewAsync(rows, pageInput, largest, cancellationToken).ConfigureAwait(false);
        return largest;
    }

    private async Task SelectPendingStatusAsync(CancellationToken cancellationToken)
    {
        ILocator status = page.GetByLabel(PamPendingSelectors.Status, new() { Exact = true });
        await status.ClickAsync(new() { Timeout = PendingControlMilliseconds })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        ILocator pending = page.GetByRole(AriaRole.Option, new() { Name = PamPendingSelectors.Pending, Exact = true });
        await pending.ClickAsync(new() { Timeout = PendingControlMilliseconds })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        bool selected = await WaitUntilAsync(async () =>
            string.Equals(await status.GetAttributeAsync("title", new() { Timeout = PendingProbeMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false), PamPendingSelectors.Pending, StringComparison.Ordinal) &&
            !await pending.IsVisibleAsync().WaitAsync(cancellationToken).ConfigureAwait(false) &&
            await status.IsEnabledAsync(new() { Timeout = PendingProbeMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        if (!selected) throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
    }

    private sealed record PendingViewPosition(int CurrentPage, int TotalPages, PamPendingItemCount Items);
    private sealed record PendingView(PendingViewPosition Position, IReadOnlyList<PamPendingRequest> Requests);

    private async Task<PendingViewPosition> ReadPendingPositionAsync(ILocator pageInput, CancellationToken cancellationToken)
    {
        int paginatorCount = await pageInput.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (paginatorCount > 1) throw new PamPendingException(PamPendingErrorCode.InvalidRequest);
        (int current, int total) = paginatorCount == 0
            ? (1, 1)
            : await ReadPendingPageInfoAsync(pageInput, cancellationToken).ConfigureAwait(false);
        PamPendingItemCount items = await ReadVisibleItemCountAsync(cancellationToken).ConfigureAwait(false);
        if (total > MaximumPendingPages) throw new PamPendingException(PamPendingErrorCode.InvalidRequest);
        pendingReadDiagnostic = pendingReadDiagnostic with { CurrentPage = current, TotalPages = total, TotalItems = items.Total };
        return new(current, total, items);
    }

    private async Task<PendingView> ReadStablePendingViewAsync(
        ILocator rows, ILocator pageInput, int? pageSize, CancellationToken cancellationToken)
    {
        PendingView? previous = null;
        Exception? lastFailure = null;
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt < PamOperationTimingPolicy.PendingControlTimeout.TotalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!await IsPendingStatusReadyAsync(cancellationToken).ConfigureAwait(false))
                {
                    previous = null;
                    await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                PendingViewPosition before = await ReadPendingPositionAsync(pageInput, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<PamPendingRequest> current = await ReadCurrentPendingPageAsync(rows, cancellationToken).ConfigureAwait(false);
                PendingViewPosition after = await ReadPendingPositionAsync(pageInput, cancellationToken).ConfigureAwait(false);
                pendingReadDiagnostic = pendingReadDiagnostic with { RowsRead = current.Count };
                lastFailure = null;
                bool complete = before == after && IsPendingPageComplete(after, current.Count, pageSize) &&
                    await IsPendingStatusReadyAsync(cancellationToken).ConfigureAwait(false);
                if (complete && previous is not null && previous.Position == after && current.SequenceEqual(previous.Requests))
                    return new(after, current);
                previous = complete ? new(after, current) : null;
            }
            catch (Exception exception) when (IsTransientPortalReadException(exception))
            {
                previous = null;
                lastFailure = exception;
            }
            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }
        throw new PamPendingException(lastFailure is ArgumentException
            ? PamPendingErrorCode.InvalidRequest : PamPendingErrorCode.SnapshotUnstable, lastFailure);
    }

    private static bool IsPendingPageComplete(PendingViewPosition position, int rows, int? pageSize)
    {
        PamPendingItemCount items = position.Items;
        if (items.Total == 0) return rows == 0 && position.CurrentPage == 1 && position.TotalPages == 1;
        if (rows == 0 || rows > items.Total) return false;
        if (items.First is int first && items.Last is int last && last - first + 1 != rows) return false;
        if (pageSize is int size)
        {
            int expectedPages = (int)(((long)items.Total + size - 1) / size);
            int expectedFirst = (position.CurrentPage - 1) * size + 1;
            int expectedRows = Math.Min(size, items.Total - expectedFirst + 1);
            if (position.TotalPages != expectedPages || rows != expectedRows ||
                (items.First is not null && items.First != expectedFirst)) return false;
        }
        return position.TotalPages != 1 || rows == items.Total;
    }

    private async Task<bool> IsPendingStatusReadyAsync(CancellationToken cancellationToken)
    {
        ILocator status = page.GetByLabel(PamPendingSelectors.Status, new() { Exact = true });
        return await status.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false) == 1 &&
            await status.IsEnabledAsync(new() { Timeout = PendingProbeMilliseconds }).WaitAsync(cancellationToken).ConfigureAwait(false) &&
            string.Equals(await status.GetAttributeAsync("title", new() { Timeout = PendingProbeMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false), PamPendingSelectors.Pending, StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<PamPendingRequest>> ScanAllPendingPagesAsync(
        ILocator rows, ILocator pageInput, int? pageSize, CancellationToken cancellationToken)
    {
        pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "ReadStablePage", NextPageEnabled = null };
        PendingView view = await ReadStablePendingViewAsync(rows, pageInput, pageSize, cancellationToken).ConfigureAwait(false);
        if (view.Position.CurrentPage != 1)
        {
            pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "FirstPage" };
            await page.GetByRole(AriaRole.Button, new() { Name = PamPendingSelectors.FirstPage, Exact = true })
                .ClickAsync(new() { Timeout = PendingControlMilliseconds }).WaitAsync(cancellationToken).ConfigureAwait(false);
            view = await WaitForPendingPageAsync(rows, pageInput, pageSize, 1, view, cancellationToken).ConfigureAwait(false);
        }
        int totalItems = view.Position.Items.Total;
        int totalPages = view.Position.TotalPages;
        var requests = new List<PamPendingRequest>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int expectedPage = 1; expectedPage <= totalPages; expectedPage++)
        {
            if (view.Position.CurrentPage != expectedPage || view.Position.TotalPages != totalPages ||
                view.Position.Items.Total != totalItems)
                throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
            foreach (PamPendingRequest request in view.Requests)
            {
                if (!ids.Add(request.RequestId)) throw new PamPendingException(PamPendingErrorCode.DuplicateRequestId);
                requests.Add(request);
            }
            if (expectedPage == totalPages) break;
            pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "NextPage" };
            // Recheck immediately before clicking; resizing can remove the next page.
            if (await ReadPendingPositionAsync(pageInput, cancellationToken).ConfigureAwait(false) != view.Position)
                throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
            ILocator next = page.GetByRole(AriaRole.Button, new() { Name = PamPendingSelectors.NextPage, Exact = true });
            bool enabled = await next.IsEnabledAsync(new() { Timeout = PendingProbeMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            pendingReadDiagnostic = pendingReadDiagnostic with { NextPageEnabled = enabled };
            if (!enabled) throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
            await next.ClickAsync(new() { Timeout = PendingControlMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            view = await WaitForPendingPageAsync(rows, pageInput, pageSize, expectedPage + 1, view, cancellationToken)
                .ConfigureAwait(false);
        }
        pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "VerifyTotal" };
        PendingViewPosition final = await ReadPendingPositionAsync(pageInput, cancellationToken).ConfigureAwait(false);
        if (final != view.Position || requests.Count != totalItems)
            throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
        return requests;
    }

    private async Task<PendingView> WaitForPendingPageAsync(
        ILocator rows, ILocator pageInput, int? pageSize, int expectedPage, PendingView previous,
        CancellationToken cancellationToken)
    {
        pendingReadDiagnostic = pendingReadDiagnostic with { Operation = "WaitPageTransition" };
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt < PamOperationTimingPolicy.PendingControlTimeout.TotalMilliseconds)
        {
            PendingView current = await ReadStablePendingViewAsync(rows, pageInput, pageSize, cancellationToken).ConfigureAwait(false);
            if (current.Position.CurrentPage == expectedPage &&
                !current.Requests.Select(static r => r.RequestId).SequenceEqual(previous.Requests.Select(static r => r.RequestId)))
                return current;
            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }
        throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
    }

    private static async Task<(int CurrentPage, int TotalPages)> ReadPendingPageInfoAsync(
        ILocator pageInput, CancellationToken cancellationToken)
    {
        string? label = await pageInput.GetAttributeAsync("aria-label", new() { Timeout = PendingProbeMilliseconds })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        return PamPendingPagination.Parse(label);
    }

    private static async Task<IReadOnlyList<PamPendingRequest>> ReadCurrentPendingPageAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        PendingGridRowSnapshot[] snapshot = await CapturePendingGridSnapshotAsync(rows, cancellationToken)
            .ConfigureAwait(false);
        return ParsePendingGridSnapshot(snapshot);
    }

    internal static async Task<PendingGridRowSnapshot[]> CapturePendingGridSnapshotAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return await rows
            .EvaluateAllAsync<PendingGridRowSnapshot[]>(PendingGridSnapshotExpression)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal static IReadOnlyList<PamPendingRequest> ParsePendingGridSnapshot(
        IReadOnlyList<PendingGridRowSnapshot> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var requests = new List<PamPendingRequest>(snapshot.Count);
        foreach (PendingGridRowSnapshot row in snapshot)
        {
            ArgumentNullException.ThrowIfNull(row);
            Dictionary<string, string> cells = CreatePendingCellMap(row.Cells);
            string requestId = ReadRequiredPendingCell(cells, PamPendingSelectors.RequestIdColumn);
            string system = ReadRequiredPendingCell(cells, PamPendingSelectors.SystemColumn);
            string account = ReadRequiredPendingCell(cells, PamPendingSelectors.AccountColumn);
            string application = ReadRequiredPendingCell(cells, PamPendingSelectors.ApplicationColumn);
            string release = ReadRequiredPendingCell(cells, PamPendingSelectors.ReleaseColumn);
            string response = ReadRequiredPendingCell(cells, PamPendingSelectors.ResponseStatusColumn);
            string expires = ReadRequiredPendingCell(cells, PamPendingSelectors.ExpiresColumn);
            string description = ReadOptionalPendingCell(cells, PamPendingSelectors.DescriptionColumn);
            (DateOnly startDate, TimeOnly startTime) = PamPendingStartParser.Parse(release);
            (DateOnly expiresDate, TimeOnly expiresTime) = PamPendingStartParser.Parse(expires);
            requests.Add(new PamPendingRequest(
                requestId,
                system,
                application,
                account,
                startDate,
                startTime,
                expiresDate,
                expiresTime,
                response,
                description));
        }

        return requests;
    }

    private static Dictionary<string, string> CreatePendingCellMap(
        IReadOnlyList<PendingGridCellSnapshot> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var result = new Dictionary<string, string>(cells.Count, StringComparer.Ordinal);
        foreach (PendingGridCellSnapshot cell in cells)
        {
            ArgumentNullException.ThrowIfNull(cell);
            string columnId = cell.ColumnId.Trim();
            if (columnId.Length == 0 || !result.TryAdd(columnId, cell.Value.Trim()))
            {
                throw new ArgumentException("La fila Pending contiene columnas técnicas inválidas o repetidas.");
            }
        }

        return result;
    }

    private static string ReadRequiredPendingCell(
        IReadOnlyDictionary<string, string> cells,
        string columnId)
    {
        if (!cells.TryGetValue(columnId, out string? value))
        {
            throw new ArgumentException("La fila Pending no contiene todas las columnas requeridas.");
        }

        if (value.Length == 0)
        {
            throw new ArgumentException("La fila Pending contiene un valor obligatorio vacío.");
        }

        return value;
    }

    private static string ReadOptionalPendingCell(
        IReadOnlyDictionary<string, string> cells,
        string columnId) => cells.TryGetValue(columnId, out string? value) ? value : string.Empty;

    internal static bool IsTransientPortalReadException(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or TimeoutException;

    private static async Task<string> ReadPendingSignatureAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PamPendingRequest> requests = await ReadCurrentPendingPageAsync(rows, cancellationToken)
            .ConfigureAwait(false);
        return string.Join('|', requests.Select(static request => request.RequestId));
    }

    private async Task<PamPendingItemCount> ReadVisibleItemCountAsync(CancellationToken cancellationToken)
    {
        ILocator counts = page.GetByText(PamPendingSelectors.ItemCountExpression());
        int count = await counts.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        PamPendingItemCount? result = null;
        for (int index = 0; index < count; index++)
        {
            ILocator candidate = counts.Nth(index);
            if (!await candidate.IsVisibleAsync().WaitAsync(cancellationToken).ConfigureAwait(false)) continue;
            string text = await candidate.InnerTextAsync(new() { Timeout = PendingProbeMilliseconds })
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            PamPendingItemCount parsed = PamPendingItemCount.Parse(text);
            if (result is not null && result != parsed)
                throw new InvalidOperationException("Pending publicó contadores contradictorios.");
            result = parsed;
        }
        return result ?? throw new InvalidOperationException("Pending no publicó el total de registros.");
    }

    private async Task CapturePendingColumnViewAsync(
        string? captureDirectory,
        string cellSelector,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (captureDirectory is null)
        {
            return;
        }

        ILocator cells = page.Locator(cellSelector);
        if (await cells.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false) == 0)
        {
            return;
        }

        await cells.First.ScrollIntoViewIfNeededAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await cells.First.HoverAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
        await CaptureAsync(captureDirectory, fileName, cancellationToken).ConfigureAwait(false);
    }

}
