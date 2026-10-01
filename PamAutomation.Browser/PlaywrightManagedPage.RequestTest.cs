using System.Globalization;
using Microsoft.Playwright;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Resilience;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private bool submissionBlockInstalled;
    private int blockedSubmissionAttempts;

    public async Task ValidateAndDiscardRequestFormAsync(
        PamFavoriteAccount account,
        RequestSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureSubmissionBlockedAsync(cancellationToken).ConfigureAwait(false);
        int attemptsBefore = Volatile.Read(ref blockedSubmissionAttempts);
        ILocator row = await FindFavoriteRowAsync(account, cancellationToken).ConfigureAwait(false);
        await OpenRequestFormAsync(row, cancellationToken).ConfigureAwait(false);
        await FillAndVerifyRequestFormAsync(
            PamRequestFormData.FromSettings(settings),
            cancellationToken).ConfigureAwait(false);
        await DiscardRequestFormAsync(cancellationToken).ConfigureAwait(false);

        if (Volatile.Read(ref blockedSubmissionAttempts) != attemptsBefore)
        {
            throw new InvalidOperationException("El modo de prueba bloqueó un intento inesperado de envío.");
        }
    }

    private async Task EnsureSubmissionBlockedAsync(CancellationToken cancellationToken)
    {
        if (submissionBlockInstalled)
        {
            return;
        }

        await page.RouteAsync(PamRequestTestSelectors.SubmissionEndpointExpression(), async route =>
        {
            if (!string.Equals(route.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await route.FallbackAsync().ConfigureAwait(false);
                return;
            }

            Interlocked.Increment(ref blockedSubmissionAttempts);
            await route.AbortAsync().ConfigureAwait(false);
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
        submissionBlockInstalled = true;
    }

    private async Task<ILocator> FindFavoriteRowAsync(
        PamFavoriteAccount expected,
        CancellationToken cancellationToken)
    {
        await page.GotoAsync(PamFavoritesSelectors.AccountsUri.AbsoluteUri)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        ILocator browseByCategory = page.GetByText(PamFavoritesSelectors.BrowseByCategoryExpression());
        await RequireUniqueVisibleAsync(browseByCategory, "Browse by Category", cancellationToken)
            .ConfigureAwait(false);
        ILocator favorites = page.GetByText(PamFavoritesSelectors.Favorites, new() { Exact = true });
        await RequireUniqueVisibleAsync(favorites, PamFavoritesSelectors.Favorites, cancellationToken)
            .ConfigureAwait(false);
        await favorites.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator rows = page.Locator(PamFavoritesSelectors.Rows);
        await rows.First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = (float)PortalTimeout.TotalMilliseconds,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator pageInput = page.Locator(PamFavoritesSelectors.PageInput);
        int paginatorCount = await pageInput.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        int totalPages = 1;
        if (paginatorCount == 1)
        {
            (int currentPage, totalPages) = await ReadPageInfoAsync(pageInput, cancellationToken)
                .ConfigureAwait(false);
            if (totalPages > MaximumFavoritePages)
            {
                throw new InvalidOperationException("Favorites excede el límite seguro de páginas.");
            }

            if (currentPage != 1)
            {
                ILocator firstPage = page.GetByRole(
                    AriaRole.Button,
                    new() { Name = PamFavoritesSelectors.FirstPage, Exact = true });
                await RequireUniqueVisibleAsync(firstPage, PamFavoritesSelectors.FirstPage, cancellationToken)
                    .ConfigureAwait(false);
                await firstPage.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                await WaitForPageAsync(pageInput, 1, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (paginatorCount != 0)
        {
            throw new InvalidOperationException("Favorites presentó más de un paginador.");
        }

        for (int pageNumber = 1; pageNumber <= totalPages; pageNumber++)
        {
            await ReadStableCurrentPageAsync(rows, cancellationToken).ConfigureAwait(false);
            ILocator? match = await FindMatchingRowOnCurrentPageAsync(rows, expected, cancellationToken)
                .ConfigureAwait(false);
            if (match is not null)
            {
                return match;
            }

            if (pageNumber < totalPages)
            {
                ILocator nextPage = page.GetByRole(
                    AriaRole.Button,
                    new() { Name = PamFavoritesSelectors.NextPage, Exact = true });
                await RequireUniqueVisibleAsync(nextPage, PamFavoritesSelectors.NextPage, cancellationToken)
                    .ConfigureAwait(false);
                await nextPage.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                await WaitForPageAsync(pageInput, pageNumber + 1, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Una cuenta de Favorites cambió antes de abrir su formulario.");
    }

    private static async Task<ILocator?> FindMatchingRowOnCurrentPageAsync(
        ILocator rows,
        PamFavoriteAccount expected,
        CancellationToken cancellationToken)
    {
        ILocator? match = null;
        int count = await rows.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < count; index++)
        {
            ILocator row = rows.Nth(index);
            var observed = new PamFavoriteAccount(
                await ReadCellAsync(
                    row,
                    PamFavoritesSelectors.SystemCell,
                    "System",
                    cancellationToken).ConfigureAwait(false),
                await ReadCellAsync(
                    row,
                    PamFavoritesSelectors.ApplicationCell,
                    "Application",
                    cancellationToken).ConfigureAwait(false),
                await ReadCellAsync(
                    row,
                    PamFavoritesSelectors.AccountCell,
                    "Account",
                    cancellationToken).ConfigureAwait(false));
            if (observed != expected)
            {
                continue;
            }

            if (match is not null)
            {
                throw new InvalidOperationException("Favorites presentó una identidad duplicada durante la prueba.");
            }

            match = row;
        }

        return match;
    }

    private async Task OpenRequestFormAsync(ILocator row, CancellationToken cancellationToken)
    {
        ILocator access = row.GetByText(PamRequestTestSelectors.Access, new() { Exact = true });
        await RequireUniqueVisibleAsync(access, PamRequestTestSelectors.Access, cancellationToken)
            .ConfigureAwait(false);
        await access.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator submitTab = page.GetByRole(
            AriaRole.Tab,
            new() { Name = PamRequestTestSelectors.SubmitRequest, Exact = true });
        await RequireUniqueVisibleAsync(submitTab, "pestaña Submit Request", cancellationToken)
            .ConfigureAwait(false);
        await submitTab.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await RequireUniqueVisibleAsync(
            page.GetByText(PamRequestTestSelectors.FormHeading, new() { Exact = true }),
            PamRequestTestSelectors.FormHeading,
            cancellationToken).ConfigureAwait(false);

        RequestFormLocators fields = GetRequestFormLocators();
        await RequireUniqueVisibleAsync(fields.StartDate, PamRequestTestSelectors.StartDate, cancellationToken)
            .ConfigureAwait(false);
        await RequireUniqueVisibleAsync(fields.StartTime, PamRequestTestSelectors.StartTime, cancellationToken)
            .ConfigureAwait(false);
        await RequireUniqueVisibleAsync(fields.Hours, PamRequestTestSelectors.Hours, cancellationToken)
            .ConfigureAwait(false);
        await RequireUniqueVisibleAsync(fields.Reason, PamRequestTestSelectors.Reason, cancellationToken)
            .ConfigureAwait(false);
        await RequireUniqueVisibleAsync(fields.ApplicationSession, PamRequestTestSelectors.ApplicationSession, cancellationToken)
            .ConfigureAwait(false);

        bool ready = await WaitUntilAsync(
            async () => string.Equals(
                await fields.ApplicationSession.GetAttributeAsync("aria-checked").ConfigureAwait(false),
                "true",
                StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);
        if (!ready)
        {
            throw new InvalidOperationException("El formulario no inicializó Application Session.");
        }
    }

    private async Task FillAndVerifyRequestFormAsync(
        PamRequestFormData form,
        CancellationToken cancellationToken)
    {
        RequestFormLocators fields = GetRequestFormLocators();
        await SelectStartDateAsync(fields.StartDate, form.StartDate, cancellationToken)
            .ConfigureAwait(false);
        string expectedTime = form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        await fields.StartTime.FillAsync(expectedTime)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
            await fields.StartTime.InputValueAsync().ConfigureAwait(false),
            expectedTime,
            StringComparison.Ordinal))
        {
            await fields.StartTime.FillAsync(expectedTime)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        await fields.Days.FillAsync(form.DurationDays.ToString(CultureInfo.InvariantCulture))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await fields.Hours.FillAsync(form.DurationHours.ToString(CultureInfo.InvariantCulture))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await fields.Minutes.FillAsync(form.DurationMinutes.ToString(CultureInfo.InvariantCulture))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await fields.Reason.FillAsync(form.Reason).WaitAsync(cancellationToken).ConfigureAwait(false);
        await VerifyRequestFormAsync(
            form,
            PamRequestFormVerificationStage.AfterFill,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task VerifyRequestFormAsync(
        PamRequestFormData form,
        PamRequestFormVerificationStage stage,
        CancellationToken cancellationToken)
    {
        PamRequestFormMismatchField lastMismatches = PamRequestFormMismatchField.None;
        int observationCount = 0;
        int readFailureCount = 0;
        PamRequestFormReadFailure lastReadFailure = PamRequestFormReadFailure.None;
        PamRequestFormDiagnosticValues? lastObserved = null;
        var expected = new PamRequestFormDiagnosticValues(
            form.StartDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
            form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            form.DurationDays.ToString(CultureInfo.InvariantCulture),
            form.DurationHours.ToString(CultureInfo.InvariantCulture),
            form.DurationMinutes.ToString(CultureInfo.InvariantCulture),
            form.Reason.Length,
            "true");
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt <
               PamOperationTimingPolicy.FormStabilityTimeout.TotalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            observationCount++;
            try
            {
                RequestFormReadResult result = await ReadRequestFormMismatchesAsync(form).ConfigureAwait(false);
                lastMismatches = result.Mismatches;
                lastObserved = result.Observed;
                if (lastMismatches == PamRequestFormMismatchField.None)
                {
                    return;
                }
            }
            catch (Exception exception) when (IsTransientPortalReadException(exception))
            {
                readFailureCount++;
                lastReadFailure = exception switch
                {
                    TimeoutException => PamRequestFormReadFailure.Timeout,
                    ArgumentException => PamRequestFormReadFailure.Argument,
                    _ => PamRequestFormReadFailure.InvalidOperation,
                };
            }

            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new PamRequestFormVerificationException(
            stage,
            lastMismatches,
            observationCount,
            readFailureCount,
            lastReadFailure,
            expected,
            lastObserved);
    }

    private async Task<RequestFormReadResult> ReadRequestFormMismatchesAsync(PamRequestFormData form)
    {
        RequestFormLocators fields = GetRequestFormLocators();
        string expectedDate = form.StartDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        string expectedTime = form.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        PamRequestFormMismatchField mismatches = PamRequestFormMismatchField.None;
        var inputOptions = new LocatorInputValueOptions
        {
            Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds,
        };
        var attributeOptions = new LocatorGetAttributeOptions
        {
            Timeout = (float)PamOperationTimingPolicy.TransientProbeTimeout.TotalMilliseconds,
        };
        string actualDate = await fields.StartDate.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualTime = await fields.StartTime.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualDays = await fields.Days.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualHours = await fields.Hours.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualMinutes = await fields.Minutes.InputValueAsync(inputOptions).ConfigureAwait(false);
        string actualReason = await fields.Reason.InputValueAsync(inputOptions).ConfigureAwait(false);
        string? actualApplicationSession = await fields.ApplicationSession
            .GetAttributeAsync("aria-checked", attributeOptions).ConfigureAwait(false);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.StartDate, actualDate, expectedDate);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.StartTime, actualTime, expectedTime);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Days, actualDays,
            form.DurationDays.ToString(CultureInfo.InvariantCulture));
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Hours, actualHours,
            form.DurationHours.ToString(CultureInfo.InvariantCulture));
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Minutes, actualMinutes,
            form.DurationMinutes.ToString(CultureInfo.InvariantCulture));
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.Reason, actualReason, form.Reason);
        mismatches = AddMismatch(mismatches, PamRequestFormMismatchField.ApplicationSession,
            actualApplicationSession, "true", StringComparison.OrdinalIgnoreCase);
        var observed = new PamRequestFormDiagnosticValues(actualDate, actualTime, actualDays, actualHours,
            actualMinutes, actualReason.Length, actualApplicationSession);
        return new RequestFormReadResult(mismatches, observed);
    }

    private sealed record RequestFormReadResult(
        PamRequestFormMismatchField Mismatches,
        PamRequestFormDiagnosticValues Observed);

    private static PamRequestFormMismatchField AddMismatch(
        PamRequestFormMismatchField mismatches,
        PamRequestFormMismatchField field,
        string? actual,
        string expected,
        StringComparison comparison = StringComparison.Ordinal)
    {
        return string.Equals(actual, expected, comparison) ? mismatches : mismatches | field;
    }

    private Task SelectStartDateAsync(
        ILocator startDate,
        DateOnly targetDate,
        CancellationToken cancellationToken) =>
        new PamCalendarDateSelector(page).SelectAsync(startDate, targetDate, cancellationToken);

    private async Task DiscardRequestFormAsync(CancellationToken cancellationToken)
    {
        ILocator cancel = page.GetByRole(
            AriaRole.Button,
            new() { Name = PamRequestTestSelectors.Cancel, Exact = true });
        await RequireUniqueVisibleAsync(cancel, PamRequestTestSelectors.Cancel, cancellationToken)
            .ConfigureAwait(false);
        await cancel.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        ILocator discard = page.GetByRole(
            AriaRole.Button,
            new() { Name = PamRequestTestSelectors.DiscardChanges, Exact = true });
        await RequireUniqueVisibleAsync(discard, PamRequestTestSelectors.DiscardChanges, cancellationToken)
            .ConfigureAwait(false);
        await discard.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await cancel.WaitForAsync(new()
        {
            State = WaitForSelectorState.Hidden,
            Timeout = (float)PortalTimeout.TotalMilliseconds,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private RequestFormLocators GetRequestFormLocators() => new(
        page.GetByLabel(PamRequestTestSelectors.StartDate, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.StartTime, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Days, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Hours, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Minutes, new() { Exact = true }),
        page.GetByLabel(PamRequestTestSelectors.Reason, new() { Exact = true }),
        page.GetByRole(AriaRole.Switch, new() { Name = PamRequestTestSelectors.ApplicationSession, Exact = true }));

    private sealed record RequestFormLocators(
        ILocator StartDate,
        ILocator StartTime,
        ILocator Days,
        ILocator Hours,
        ILocator Minutes,
        ILocator Reason,
        ILocator ApplicationSession);
}
