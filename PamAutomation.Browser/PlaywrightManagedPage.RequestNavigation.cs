using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Resilience;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private static float FavoritesControlTimeout =>
        (float)PamOperationTimingPolicy.FavoritesControlTimeout.TotalMilliseconds;

    // Only reversible navigation belongs here. Filling, validation and Submit stay outside recovery.
    internal async Task PrepareRequestFormAsync(PamFavoriteAccount expected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(PamOperationTimingPolicy.FavoritesNavigationTimeout);
        try
        {
            for (int attempt = 0; attempt < PamOperationTimingPolicy.FavoritesNavigationMaximumAttempts; attempt++)
            {
                budget.Token.ThrowIfCancellationRequested();
                try
                {
                    if (attempt > 0 || !await CanReuseFavoritesAsync(budget.Token).ConfigureAwait(false))
                    {
                        await NavigateToRequestFavoritesAsync(budget.Token).ConfigureAwait(false);
                    }

                    ILocator? row = await FindFavoriteRowAsync(expected, budget.Token).ConfigureAwait(false);
                    if (row is null)
                    {
                        // A filter or stale view may hide the account. Reload once, never substitute a similar row.
                        if (attempt + 1 < PamOperationTimingPolicy.FavoritesNavigationMaximumAttempts) continue;
                        throw new InvalidOperationException("Una cuenta de Favorites cambió antes de abrir su formulario.");
                    }

                    await OpenRequestFormAsync(row, budget.Token).ConfigureAwait(false);
                    return;
                }
                catch (Exception exception) when (
                    attempt + 1 < PamOperationTimingPolicy.FavoritesNavigationMaximumAttempts &&
                    !budget.IsCancellationRequested && !page.IsClosed &&
                    exception is TimeoutException or PlaywrightException)
                {
                    // No Submit action has occurred in this scope. A fresh view is safe.
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && budget.IsCancellationRequested)
        {
            throw new TimeoutException("Favorites no pudo preparar el formulario dentro del tiempo permitido.");
        }
    }

    private ILocator SelectedFavoritesCategory => page.Locator(".text.selected")
        .Filter(new() { Has = page.GetByText(PamFavoritesSelectors.Favorites, new() { Exact = true }) });

    private async Task<bool> CanReuseFavoritesAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(page.Url, UriKind.Absolute, out Uri? current) ||
            current != PamFavoritesSelectors.AccountsUri)
        {
            return false;
        }

        long started = Environment.TickCount64;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsUniqueVisibleAsync(SelectedFavoritesCategory).ConfigureAwait(false) &&
                await IsUniqueVisibleAsync(page.GetByText(PamFavoritesSelectors.BrowseByCategoryExpression())).ConfigureAwait(false) &&
                !await IsAnyVisibleAsync(page.GetByRole(AriaRole.Tab,
                    new() { Name = PamRequestTestSelectors.SubmitRequest, Exact = true })).ConfigureAwait(false) &&
                !await IsAnyVisibleAsync(page.GetByText(PamRequestTestSelectors.FormHeading,
                    new() { Exact = true })).ConfigureAwait(false) &&
                await page.Locator(PamFavoritesSelectors.Rows).First.IsVisibleAsync().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }
        while (Environment.TickCount64 - started < PamOperationTimingPolicy.FavoritesReuseTimeout.TotalMilliseconds);
        return false;
    }

    private async Task NavigateToRequestFavoritesAsync(CancellationToken cancellationToken)
    {
        await page.GotoAsync(PamFavoritesSelectors.AccountsUri.AbsoluteUri,
            new() { Timeout = FavoritesControlTimeout, WaitUntil = WaitUntilState.DOMContentLoaded })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        await RequireNavigationControlAsync(page.GetByText(PamFavoritesSelectors.BrowseByCategoryExpression()),
            cancellationToken).ConfigureAwait(false);
        ILocator favorites = page.GetByText(PamFavoritesSelectors.Favorites, new() { Exact = true });
        await ClickNavigationControlAsync(favorites, cancellationToken).ConfigureAwait(false);
        await RequireNavigationControlAsync(SelectedFavoritesCategory, cancellationToken).ConfigureAwait(false);
        await page.Locator(PamFavoritesSelectors.Rows).First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = FavoritesControlTimeout,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ILocator?> FindFavoriteRowAsync(PamFavoriteAccount expected, CancellationToken cancellationToken)
    {
        ILocator rows = page.Locator(PamFavoritesSelectors.Rows);
        ILocator pageInput = page.Locator(PamFavoritesSelectors.PageInput);
        (int current, int total) = await ReadRequestFavoritesPageAsync(pageInput, cancellationToken).ConfigureAwait(false);

        // Search the current page first. Do not reset pagination for every request.
        for (int visited = 0; visited < total; visited++)
        {
            await ReadStableCurrentPageAsync(rows, cancellationToken).ConfigureAwait(false);
            if (!await IsUniqueVisibleAsync(SelectedFavoritesCategory).ConfigureAwait(false))
                throw new TimeoutException("Favorites dejó de ser la categoría seleccionada.");
            if (await ReadRequestFavoritesPageAsync(pageInput, cancellationToken).ConfigureAwait(false) != (current, total))
                throw new TimeoutException("La paginación de Favorites cambió durante la búsqueda.");

            ILocator match = MatchFavoriteIdentity(rows, expected);
            int count = await match.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (count > 1)
                throw new InvalidOperationException("Favorites presentó una identidad duplicada antes de abrir su formulario.");
            if (count == 1) return match;

            if (visited + 1 == total) break;
            bool wrap = current == total;
            int next = wrap ? 1 : current + 1;
            ILocator control = page.GetByRole(AriaRole.Button, new()
            {
                Name = wrap ? PamFavoritesSelectors.FirstPage : PamFavoritesSelectors.NextPage,
                Exact = true,
            });
            await ClickNavigationControlAsync(control, cancellationToken).ConfigureAwait(false);
            await WaitForPageAsync(pageInput, next, cancellationToken).ConfigureAwait(false);
            current = next;
        }

        return null;
    }

    private static async Task<(int Current, int Total)> ReadRequestFavoritesPageAsync(
        ILocator input, CancellationToken cancellationToken)
    {
        int count = await input.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (count == 0) return (1, 1);
        if (count != 1) throw new InvalidOperationException("Favorites presentó más de un paginador.");
        (int current, int total) = await ReadPageInfoAsync(input, cancellationToken).ConfigureAwait(false);
        if (total > MaximumFavoritePages)
            throw new InvalidOperationException("Favorites excede el límite seguro de páginas.");
        return (current, total);
    }

    // Resolve all three exact cells on every action, including Click's automatic re-resolution.
    // A row index would silently target another account if the grid reorders after the read.
    private ILocator MatchFavoriteIdentity(ILocator rows, PamFavoriteAccount expected) =>
        rows.Filter(new() { Has = ExactCell(PamFavoritesSelectors.SystemCell, expected.SystemName) })
            .Filter(new() { Has = ExactCell(PamFavoritesSelectors.ApplicationCell, expected.ApplicationName) })
            .Filter(new() { Has = ExactCell(PamFavoritesSelectors.AccountCell, expected.AccountName) });

    private ILocator ExactCell(string selector, string value) => page.Locator(selector).Filter(new()
    {
        HasTextRegex = new Regex(@"^\s*" + Regex.Escape(value) + @"\s*$"),
    });

    private static async Task RequireNavigationControlAsync(ILocator locator, CancellationToken cancellationToken)
    {
        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = FavoritesControlTimeout })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
        if (await locator.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("PAM presentó un control de navegación ambiguo.");
    }

    private static async Task ClickNavigationControlAsync(ILocator locator, CancellationToken cancellationToken)
    {
        await RequireNavigationControlAsync(locator, cancellationToken).ConfigureAwait(false);
        await locator.ClickAsync(new() { Timeout = FavoritesControlTimeout })
            .WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task OpenRequestFormAsync(ILocator row, CancellationToken cancellationToken)
    {
        await ClickNavigationControlAsync(
            row.GetByText(PamRequestTestSelectors.Access, new() { Exact = true }), cancellationToken).ConfigureAwait(false);
        await ClickNavigationControlAsync(page.GetByRole(AriaRole.Tab,
            new() { Name = PamRequestTestSelectors.SubmitRequest, Exact = true }), cancellationToken).ConfigureAwait(false);
        await RequireNavigationControlAsync(
            page.GetByText(PamRequestTestSelectors.FormHeading, new() { Exact = true }), cancellationToken).ConfigureAwait(false);

        RequestFormLocators fields = GetRequestFormLocators();
        foreach (ILocator field in new[] { fields.StartDate, fields.StartTime, fields.Hours, fields.Reason, fields.ApplicationSession })
            await RequireNavigationControlAsync(field, cancellationToken).ConfigureAwait(false);

        long started = Environment.TickCount64;
        while (Environment.TickCount64 - started < FavoritesControlTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(await fields.ApplicationSession.GetAttributeAsync("aria-checked",
                    new() { Timeout = FavoritesControlTimeout }).WaitAsync(cancellationToken).ConfigureAwait(false),
                "true", StringComparison.OrdinalIgnoreCase)) return;
            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("El formulario no inicializó Application Session.");
    }
}
