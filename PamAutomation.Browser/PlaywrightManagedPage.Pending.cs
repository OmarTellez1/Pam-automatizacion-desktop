using Microsoft.Playwright;
using PamAutomation.Core.Resilience;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private const int MaximumPendingPages = 500;
    private const string PendingCaptureName = "fase13-pending-grid.png";

    public async Task<IReadOnlyList<PamPendingRequest>> ReadAllPendingAsync(
        string? captureDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await page.GotoAsync(PamPendingSelectors.RequestsUri.AbsoluteUri)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await SelectPendingStatusAsync(cancellationToken).ConfigureAwait(false);

        ILocator rows = page.Locator(PamPendingSelectors.Rows);
        ILocator pageInput = page.Locator(PamPendingSelectors.PageInput);
        int paginatorCount = await pageInput.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (paginatorCount == 0)
        {
            await CapturePendingViewsAsync(captureDirectory, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<PamPendingRequest> current = await ReadStablePendingPageAsync(rows, cancellationToken)
                .ConfigureAwait(false);
            int? expectedCount = await ReadVisibleItemCountAsync(cancellationToken).ConfigureAwait(false);
            if (expectedCount is not null && expectedCount != current.Count)
            {
                throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
            }

            return current;
        }

        if (paginatorCount != 1)
        {
            throw new InvalidOperationException("Pending presentó más de un paginador.");
        }

        await SelectLargestPendingPageSizeAsync(pageInput, cancellationToken)
            .ConfigureAwait(false);
        await CapturePendingViewsAsync(captureDirectory, cancellationToken).ConfigureAwait(false);

        for (int scanAttempt = 1;
             scanAttempt <= PamOperationTimingPolicy.GridScanMaximumAttempts;
             scanAttempt++)
        {
            IReadOnlyList<PamPendingRequest> requests = await ScanAllPendingPagesAsync(
                rows,
                pageInput,
                cancellationToken).ConfigureAwait(false);
            int? expectedCount = await ReadVisibleItemCountAsync(cancellationToken).ConfigureAwait(false);
            if (expectedCount is null || expectedCount == requests.Count)
            {
                return requests;
            }

            if (scanAttempt < PamOperationTimingPolicy.GridScanMaximumAttempts)
            {
                await Task.Delay(
                    PamOperationTimingPolicy.GridScanRetryDelay,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
    }

    private async Task CapturePendingViewsAsync(
        string? captureDirectory,
        CancellationToken cancellationToken)
    {
        await CaptureAsync(captureDirectory, PendingCaptureName, cancellationToken).ConfigureAwait(false);
        await CapturePendingColumnViewAsync(
            captureDirectory,
            PamPendingSelectors.DescriptionCell,
            "fase13-pending-grid-description-and-expiration.png",
            cancellationToken).ConfigureAwait(false);
        await CapturePendingColumnViewAsync(
            captureDirectory,
            PamPendingSelectors.ApplicationCell,
            "fase13-pending-grid-application.png",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SelectLargestPendingPageSizeAsync(
        ILocator pageInput,
        CancellationToken cancellationToken)
    {
        ILocator labels = page.GetByText(PamPendingSelectors.ItemsPerPageExpression());
        ILocator? visibleLabel = null;
        int labelCount = await labels.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < labelCount; index++)
        {
            ILocator candidate = labels.Nth(index);
            if (await candidate.IsVisibleAsync().ConfigureAwait(false))
            {
                if (visibleLabel is not null)
                {
                    throw new InvalidOperationException("Pending presentó más de un selector de filas por página.");
                }

                visibleLabel = candidate;
            }
        }

        // Older PAM variants may not expose this selector. Their paginated traversal remains supported.
        if (visibleLabel is null)
        {
            return;
        }

        ILocator selectorRegion = visibleLabel.Locator(
            "xpath=ancestor::*[.//*[@role='combobox']][1]");
        ILocator selectors = selectorRegion.GetByRole(AriaRole.Combobox);
        int selectorCount = await selectors.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (selectorCount != 1)
        {
            throw new InvalidOperationException("Pending no presentó un selector único de filas por página.");
        }

        ILocator selector = selectors.First;
        await selector.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator options = page.GetByRole(AriaRole.Option);
        ILocator? largestOption = null;
        int largestPageSize = 0;
        int optionCount = await options.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < optionCount; index++)
        {
            ILocator option = options.Nth(index);
            if (!await option.IsVisibleAsync().ConfigureAwait(false))
            {
                continue;
            }

            string text = (await option.InnerTextAsync().WaitAsync(cancellationToken).ConfigureAwait(false)).Trim();
            if (int.TryParse(text, out int pageSize) && pageSize > largestPageSize)
            {
                largestOption = option;
                largestPageSize = pageSize;
            }
        }

        if (largestOption is null)
        {
            await page.Keyboard.PressAsync("Escape").WaitAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Pending no presentó opciones válidas de filas por página.");
        }

        int? itemCount = await ReadVisibleItemCountAsync(cancellationToken).ConfigureAwait(false);
        await largestOption.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        if (itemCount is null)
        {
            return;
        }

        int expectedPages = Math.Max(1, (itemCount.Value + largestPageSize - 1) / largestPageSize);
        bool pageSizeConfirmed = await WaitUntilAsync(async () =>
        {
            try
            {
                (_, int totalPages) = await ReadPendingPageInfoAsync(pageInput, cancellationToken)
                    .ConfigureAwait(false);
                return totalPages == expectedPages;
            }
            catch (Exception exception) when (IsTransientPortalReadException(exception))
            {
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
        if (!pageSizeConfirmed)
        {
            throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
        }
    }

    private async Task SelectPendingStatusAsync(CancellationToken cancellationToken)
    {
        ILocator status = page.GetByLabel(PamPendingSelectors.Status, new() { Exact = true });
        await RequireUniqueVisibleAsync(status, PamPendingSelectors.Status, cancellationToken)
            .ConfigureAwait(false);
        await status.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator pending = page.GetByRole(
            AriaRole.Option,
            new() { Name = PamPendingSelectors.Pending, Exact = true });
        await RequireUniqueVisibleAsync(pending, PamPendingSelectors.Pending, cancellationToken)
            .ConfigureAwait(false);
        await pending.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        bool selected = await WaitUntilAsync(
            async () =>
            {
                bool titleConfirmed = string.Equals(
                    await status.GetAttributeAsync("title").ConfigureAwait(false),
                    PamPendingSelectors.Pending,
                    StringComparison.Ordinal);
                return titleConfirmed &&
                    !await pending.IsVisibleAsync().ConfigureAwait(false) &&
                    await status.IsEnabledAsync().ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        if (!selected)
        {
            throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
        }
    }

    private async Task<IReadOnlyList<PamPendingRequest>> ScanAllPendingPagesAsync(
        ILocator rows,
        ILocator pageInput,
        CancellationToken cancellationToken)
    {
        (int currentPage, int totalPages) = await ReadPendingPageInfoAsync(pageInput, cancellationToken)
            .ConfigureAwait(false);
        if (totalPages > MaximumPendingPages)
        {
            throw new InvalidOperationException("Pending excede el límite seguro de páginas.");
        }

        if (currentPage != 1)
        {
            string previousSignature = await ReadPendingSignatureAsync(rows, cancellationToken).ConfigureAwait(false);
            ILocator firstPage = page.GetByRole(
                AriaRole.Button,
                new() { Name = PamPendingSelectors.FirstPage, Exact = true });
            await RequireUniqueVisibleAsync(firstPage, PamPendingSelectors.FirstPage, cancellationToken)
                .ConfigureAwait(false);
            await firstPage.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            await WaitForPendingPageAsync(pageInput, rows, 1, previousSignature, cancellationToken)
                .ConfigureAwait(false);
        }

        var requests = new List<PamPendingRequest>();
        var requestIds = new HashSet<string>(StringComparer.Ordinal);
        for (int expectedPage = 1; expectedPage <= totalPages; expectedPage++)
        {
            (currentPage, int observedTotalPages) = await ReadPendingPageInfoAsync(pageInput, cancellationToken)
                .ConfigureAwait(false);
            if (currentPage != expectedPage || observedTotalPages != totalPages)
            {
                throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
            }

            IReadOnlyList<PamPendingRequest> current = await ReadStablePendingPageAsync(rows, cancellationToken)
                .ConfigureAwait(false);
            foreach (PamPendingRequest request in current)
            {
                if (!requestIds.Add(request.RequestId))
                {
                    throw new PamPendingException(PamPendingErrorCode.DuplicateRequestId);
                }

                requests.Add(request);
            }

            if (expectedPage == totalPages)
            {
                break;
            }

            string previousSignature = string.Join('|', current.Select(static request => request.RequestId));
            ILocator nextPage = page.GetByRole(
                AriaRole.Button,
                new() { Name = PamPendingSelectors.NextPage, Exact = true });
            await RequireUniqueVisibleAsync(nextPage, PamPendingSelectors.NextPage, cancellationToken)
                .ConfigureAwait(false);
            await nextPage.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            await WaitForPendingPageAsync(
                pageInput,
                rows,
                expectedPage + 1,
                previousSignature,
                cancellationToken).ConfigureAwait(false);
        }

        return requests;
    }

    private static async Task<(int CurrentPage, int TotalPages)> ReadPendingPageInfoAsync(
        ILocator pageInput,
        CancellationToken cancellationToken)
    {
        string? label = await pageInput.GetAttributeAsync("aria-label")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return PamPendingPagination.Parse(label);
    }

    private static async Task WaitForPendingPageAsync(
        ILocator pageInput,
        ILocator rows,
        int expectedPage,
        string previousSignature,
        CancellationToken cancellationToken)
    {
        bool observed = await WaitUntilAsync(async () =>
        {
            try
            {
                (int currentPage, _) = await ReadPendingPageInfoAsync(pageInput, cancellationToken)
                    .ConfigureAwait(false);
                if (currentPage != expectedPage)
                {
                    return false;
                }

                string signature = await ReadPendingSignatureAsync(rows, cancellationToken).ConfigureAwait(false);
                return !string.Equals(signature, previousSignature, StringComparison.Ordinal);
            }
            catch (Exception exception) when (IsTransientPortalReadException(exception))
            {
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
        if (!observed)
        {
            throw new PamPendingException(PamPendingErrorCode.SnapshotUnstable);
        }
    }

    private static async Task<IReadOnlyList<PamPendingRequest>> ReadStablePendingPageAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PamPendingRequest>? previous = null;
        Exception? lastReadFailure = null;
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt < PortalTimeout.TotalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<PamPendingRequest> current = await ReadCurrentPendingPageAsync(rows, cancellationToken)
                    .ConfigureAwait(false);
                lastReadFailure = null;
                if (previous is not null && current.SequenceEqual(previous))
                {
                    return current;
                }

                previous = current;
            }
            catch (Exception exception) when (IsTransientPortalReadException(exception))
            {
                previous = null;
                lastReadFailure = exception;
            }

            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        // An invalid row that never became readable is terminal after the local probe window.
        throw new PamPendingException(
            lastReadFailure is ArgumentException or InvalidOperationException
                ? PamPendingErrorCode.InvalidRequest
                : PamPendingErrorCode.SnapshotUnstable,
            lastReadFailure);
    }

    private static async Task<IReadOnlyList<PamPendingRequest>> ReadCurrentPendingPageAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        int count = await rows.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        var requests = new List<PamPendingRequest>(count);
        for (int index = 0; index < count; index++)
        {
            ILocator row = rows.Nth(index);
            string requestId = await ReadPendingCellAsync(row, PamPendingSelectors.RequestIdCell, cancellationToken)
                .ConfigureAwait(false);
            string system = await ReadPendingCellAsync(row, PamPendingSelectors.SystemCell, cancellationToken)
                .ConfigureAwait(false);
            string account = await ReadPendingCellAsync(row, PamPendingSelectors.AccountCell, cancellationToken)
                .ConfigureAwait(false);
            string application = await ReadPendingCellAsync(row, PamPendingSelectors.ApplicationCell, cancellationToken)
                .ConfigureAwait(false);
            string release = await ReadPendingCellAsync(row, PamPendingSelectors.ReleaseCell, cancellationToken)
                .ConfigureAwait(false);
            string response = await ReadPendingCellAsync(
                row,
                PamPendingSelectors.ResponseStatusCell,
                cancellationToken).ConfigureAwait(false);
            string expires = await ReadPendingCellAsync(
                row,
                PamPendingSelectors.ExpiresCell,
                cancellationToken).ConfigureAwait(false);
            string description = await ReadPendingCellAsync(
                row,
                PamPendingSelectors.DescriptionCell,
                cancellationToken,
                allowEmpty: true,
                allowMissing: true).ConfigureAwait(false);
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

    private static async Task<string> ReadPendingCellAsync(
        ILocator row,
        string selector,
        CancellationToken cancellationToken,
        bool allowEmpty = false,
        bool allowMissing = false)
    {
        ILocator cell = row.Locator(selector);
        int count = await cell.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (allowMissing && count == 0)
        {
            return string.Empty;
        }

        if (count != 1)
        {
            throw new ArgumentException("La fila Pending no contiene todas las columnas requeridas.");
        }

        string value = (await cell.InnerTextAsync(new LocatorInnerTextOptions
        {
            Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds,
        }).WaitAsync(cancellationToken).ConfigureAwait(false)).Trim();
        if (!allowEmpty && value.Length == 0)
        {
            throw new ArgumentException("La fila Pending contiene un valor obligatorio vacío.");
        }

        return value;
    }

    internal static bool IsTransientPortalReadException(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or TimeoutException;

    private static async Task<string> ReadPendingSignatureAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        int count = await rows.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        var values = new string[count];
        for (int index = 0; index < count; index++)
        {
            values[index] = await ReadPendingCellAsync(
                rows.Nth(index),
                PamPendingSelectors.RequestIdCell,
                cancellationToken).ConfigureAwait(false);
        }

        return string.Join('|', values);
    }

    private async Task<int?> ReadVisibleItemCountAsync(CancellationToken cancellationToken)
    {
        ILocator counts = page.GetByText(PamPendingSelectors.ItemCountExpression());
        int count = await counts.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        int? maximum = null;
        for (int index = 0; index < count; index++)
        {
            ILocator candidate = counts.Nth(index);
            if (!await candidate.IsVisibleAsync().ConfigureAwait(false))
            {
                continue;
            }

            string text = (await candidate.InnerTextAsync().WaitAsync(cancellationToken).ConfigureAwait(false)).Trim();
            if (int.TryParse(
                PamPendingSelectors.ItemCountExpression().Match(text).Groups[1].Value,
                out int parsed))
            {
                maximum = maximum is null ? parsed : Math.Max(maximum.Value, parsed);
            }
        }

        return maximum;
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
