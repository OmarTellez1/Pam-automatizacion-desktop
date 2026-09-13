using Microsoft.Playwright;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Browser;

internal sealed partial class PlaywrightManagedPage
{
    private const int MaximumFavoritePages = 500;

    public async Task<IReadOnlyList<PamFavoriteAccount>> ReadAllFavoritesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await page.GotoAsync(PamFavoritesSelectors.AccountsUri.AbsoluteUri)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        ILocator browseByCategory = page.GetByText(PamFavoritesSelectors.BrowseByCategoryExpression());
        await RequireUniqueVisibleAsync(browseByCategory, "Browse by Category", cancellationToken)
            .ConfigureAwait(false);

        ILocator favorites = page.GetByText(
            PamFavoritesSelectors.Favorites,
            new() { Exact = true });
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
        if (paginatorCount == 0)
        {
            return await ReadStableCurrentPageAsync(rows, cancellationToken).ConfigureAwait(false);
        }

        if (paginatorCount != 1)
        {
            throw new InvalidOperationException("Favorites presentó más de un paginador.");
        }

        (int currentPage, int totalPages) = await ReadPageInfoAsync(pageInput, cancellationToken)
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
            await WaitForPageAsync(pageInput, expectedPage: 1, cancellationToken).ConfigureAwait(false);
        }

        var accounts = new List<PamFavoriteAccount>();
        for (int expectedPage = 1; expectedPage <= totalPages; expectedPage++)
        {
            (currentPage, int observedTotalPages) = await ReadPageInfoAsync(pageInput, cancellationToken)
                .ConfigureAwait(false);
            if (currentPage != expectedPage || observedTotalPages != totalPages)
            {
                throw new InvalidOperationException("La paginación de Favorites cambió durante la lectura.");
            }

            accounts.AddRange(await ReadStableCurrentPageAsync(rows, cancellationToken).ConfigureAwait(false));
            if (expectedPage == totalPages)
            {
                break;
            }

            ILocator nextPage = page.GetByRole(
                AriaRole.Button,
                new() { Name = PamFavoritesSelectors.NextPage, Exact = true });
            await RequireUniqueVisibleAsync(nextPage, PamFavoritesSelectors.NextPage, cancellationToken)
                .ConfigureAwait(false);
            await nextPage.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            await WaitForPageAsync(pageInput, expectedPage + 1, cancellationToken).ConfigureAwait(false);
        }

        return accounts;
    }

    private static async Task<(int CurrentPage, int TotalPages)> ReadPageInfoAsync(
        ILocator pageInput,
        CancellationToken cancellationToken)
    {
        string? label = await pageInput.GetAttributeAsync("aria-label")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return PamFavoritesPagination.Parse(label);
    }

    private static async Task WaitForPageAsync(
        ILocator pageInput,
        int expectedPage,
        CancellationToken cancellationToken)
    {
        bool observed = await WaitUntilAsync(async () =>
        {
            try
            {
                (int currentPage, _) = await ReadPageInfoAsync(pageInput, cancellationToken)
                    .ConfigureAwait(false);
                return currentPage == expectedPage;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);

        if (!observed)
        {
            throw new InvalidOperationException("Favorites no confirmó el cambio de página.");
        }
    }

    private static async Task<IReadOnlyList<PamFavoriteAccount>> ReadStableCurrentPageAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PamFavoriteAccount>? previous = null;
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt < PortalTimeout.TotalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<PamFavoriteAccount> current = await ReadCurrentPageAsync(rows, cancellationToken)
                    .ConfigureAwait(false);
                if (current.Count > 0 && previous is not null && current.SequenceEqual(previous))
                {
                    return current;
                }

                previous = current;
            }
            catch (ArgumentException)
            {
                previous = null;
            }

            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new PamFavoritesException(PamFavoritesErrorCode.InvalidAccount);
    }

    private static async Task<IReadOnlyList<PamFavoriteAccount>> ReadCurrentPageAsync(
        ILocator rows,
        CancellationToken cancellationToken)
    {
        int count = await rows.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        var accounts = new List<PamFavoriteAccount>(count);
        for (int index = 0; index < count; index++)
        {
            ILocator row = rows.Nth(index);
            string system = await ReadCellAsync(row, PamFavoritesSelectors.SystemCell, cancellationToken)
                .ConfigureAwait(false);
            string application = await ReadCellAsync(row, PamFavoritesSelectors.ApplicationCell, cancellationToken)
                .ConfigureAwait(false);
            string account = await ReadCellAsync(row, PamFavoritesSelectors.AccountCell, cancellationToken)
                .ConfigureAwait(false);
            accounts.Add(new PamFavoriteAccount(system, application, account));
        }

        return accounts;
    }

    private static async Task<string> ReadCellAsync(
        ILocator row,
        string selector,
        CancellationToken cancellationToken)
    {
        ILocator cell = row.Locator(selector);
        int count = await cell.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (count != 1)
        {
            throw new ArgumentException("La fila de Favorites no contiene una identidad única y completa.");
        }

        return (await cell.InnerTextAsync().WaitAsync(cancellationToken).ConfigureAwait(false)).Trim();
    }
}
