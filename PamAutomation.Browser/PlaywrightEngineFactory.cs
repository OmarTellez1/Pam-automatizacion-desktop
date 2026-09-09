using Microsoft.Playwright;

namespace PamAutomation.Browser;

internal sealed class PlaywrightEngineFactory : IPlaywrightEngineFactory
{
    /// <inheritdoc />
    public async Task<IPlaywrightEngine> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        return new PlaywrightEngine(playwright);
    }
}

internal sealed class PlaywrightEngine(IPlaywright playwright) : IPlaywrightEngine
{
    public async Task<IManagedBrowser> LaunchEdgeAsync(EdgeLaunchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = options.Channel,
            Headless = options.Headless,
        }).ConfigureAwait(false);

        return new PlaywrightManagedBrowser(browser);
    }

    public ValueTask DisposeAsync()
    {
        playwright.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class PlaywrightManagedBrowser(IBrowser browser) : IManagedBrowser
{
    public async Task<IManagedBrowserContext> NewContextAsync(
        PamBrowserContextOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = options.IgnoreHttpSErrors,
            ServiceWorkers = options.BlockServiceWorkers ? ServiceWorkerPolicy.Block : ServiceWorkerPolicy.Allow,
        }).ConfigureAwait(false);

        return new PlaywrightManagedBrowserContext(context);
    }

    public async ValueTask DisposeAsync()
    {
        await browser.CloseAsync().ConfigureAwait(false);
    }
}

internal sealed class PlaywrightManagedBrowserContext(IBrowserContext context) : IManagedBrowserContext
{
    public Task InstallRequestPolicyAsync(Func<string, bool> shouldAbortRequest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shouldAbortRequest);
        cancellationToken.ThrowIfCancellationRequested();

        return InstallAsync();

        async Task InstallAsync()
        {
            await context.RouteAsync("**/*", async route =>
            {
                if (shouldAbortRequest(route.Request.Url))
                {
                    await route.AbortAsync().ConfigureAwait(false);
                    return;
                }

                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public async Task<IManagedPage> NewPageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IPage page = await context.NewPageAsync().ConfigureAwait(false);
        return new PlaywrightManagedPage(page);
    }

    public async ValueTask DisposeAsync()
    {
        await context.CloseAsync().ConfigureAwait(false);
    }

}

internal sealed class PlaywrightManagedPage(IPage page) : IManagedPage
{
    public async Task<PamAutomation.Core.Browser.PamNavigationResponse> NavigateAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IResponse? response = await page.GotoAsync(uri.AbsoluteUri).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        string? httpDateHeader = response is null
            ? null
            : await response.HeaderValueAsync("date").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new PamAutomation.Core.Browser.PamNavigationResponse(
            response is null ? uri : new Uri(response.Url),
            response?.Status,
            httpDateHeader,
            TimeSpan.Zero);
    }

    public async ValueTask DisposeAsync()
    {
        await page.CloseAsync().ConfigureAwait(false);
    }
}
