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

internal sealed class PlaywrightManagedPage(IPage page) : IManagedPage, IPamLoginPage
{
    private static readonly TimeSpan PortalTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(100);

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

    public async Task<bool> SubmitCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        ILocator usernameField = page.GetByRole(AriaRole.Textbox, new() { Name = PamLoginSelectors.Username, Exact = true });
        ILocator passwordField = page.GetByRole(AriaRole.Textbox, new() { Name = PamLoginSelectors.Password, Exact = true });
        ILocator loginButton = page.GetByRole(AriaRole.Button, new() { Name = PamLoginSelectors.LogIn, Exact = true });
        ILocator responseField = page.GetByRole(AriaRole.Textbox, new() { Name = PamLoginSelectors.EnterResponse, Exact = true });
        ILocator rejection = page.GetByRole(AriaRole.Alert);

        await RequireUniqueVisibleAsync(usernameField, PamLoginSelectors.Username, cancellationToken).ConfigureAwait(false);
        await RequireUniqueVisibleAsync(passwordField, PamLoginSelectors.Password, cancellationToken).ConfigureAwait(false);
        await RequireUniqueVisibleAsync(loginButton, PamLoginSelectors.LogIn, cancellationToken).ConfigureAwait(false);
        await usernameField.FillAsync(username).WaitAsync(cancellationToken).ConfigureAwait(false);
        await passwordField.FillAsync(password).WaitAsync(cancellationToken).ConfigureAwait(false);
        await loginButton.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        return await WaitForCredentialOutcomeAsync(responseField, rejection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SubmitTotpAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        ILocator responseField = page.GetByRole(AriaRole.Textbox, new() { Name = PamLoginSelectors.EnterResponse, Exact = true });
        ILocator submitButton = page.GetByRole(AriaRole.Button, new() { Name = PamLoginSelectors.Submit, Exact = true });
        ILocator rejection = page.GetByText(PamLoginSelectors.TokenRejectedExpression());

        await RequireUniqueVisibleAsync(responseField, PamLoginSelectors.EnterResponse, cancellationToken).ConfigureAwait(false);
        await RequireUniqueVisibleAsync(submitButton, PamLoginSelectors.Submit, cancellationToken).ConfigureAwait(false);
        await submitButton.ClickAsync(new LocatorClickOptions { Trial = true }).WaitAsync(cancellationToken).ConfigureAwait(false);
        await responseField.FillAsync(code).WaitAsync(cancellationToken).ConfigureAwait(false);
        await submitButton.ClickAsync().WaitAsync(cancellationToken).ConfigureAwait(false);

        return await WaitForTotpOutcomeAsync(rejection, cancellationToken).ConfigureAwait(false);
    }

    public async Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken)
    {
        ILocator rejection = page.GetByText(PamLoginSelectors.TokenRejectedExpression());
        await rejection.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = (float)PortalTimeout.TotalMilliseconds,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken)
    {
        ILocator dashboardLink = page.GetByRole(
            AriaRole.Link,
            new() { Name = PamLoginSelectors.ReturnToDashboard, Exact = true });

        return await WaitUntilAsync(
            async () => await IsUniqueVisibleAsync(dashboardLink).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> WaitForCredentialOutcomeAsync(
        ILocator responseField,
        ILocator rejection,
        CancellationToken cancellationToken)
    {
        bool? result = null;
        bool observed = await WaitUntilAsync(async () =>
        {
            if (await IsUniqueVisibleAsync(responseField).ConfigureAwait(false))
            {
                result = true;
                return true;
            }

            if (await IsCredentialRejectionVisibleAsync(rejection).ConfigureAwait(false))
            {
                result = false;
                return true;
            }

            return false;
        }, cancellationToken).ConfigureAwait(false);

        if (!observed || result is null)
        {
            throw new InvalidOperationException("El portal PAM no confirmó el resultado de las credenciales.");
        }

        return result.Value;
    }

    private async Task<bool> WaitForTotpOutcomeAsync(ILocator rejection, CancellationToken cancellationToken)
    {
        bool? result = null;
        bool observed = await WaitUntilAsync(async () =>
        {
            if (await IsAnyVisibleAsync(rejection).ConfigureAwait(false))
            {
                result = false;
                return true;
            }

            if (Uri.TryCreate(page.Url, UriKind.Absolute, out Uri? current) &&
                string.Equals(current.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(current.Host, "10.30.17.117", StringComparison.OrdinalIgnoreCase) &&
                current.Port == 443 &&
                !current.AbsolutePath.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            return false;
        }, cancellationToken).ConfigureAwait(false);

        if (!observed || result is null)
        {
            throw new InvalidOperationException("El portal PAM no confirmó el resultado del código TOTP.");
        }

        return result.Value;
    }

    private static async Task RequireUniqueVisibleAsync(
        ILocator locator,
        string description,
        CancellationToken cancellationToken)
    {
        await locator.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = (float)PortalTimeout.TotalMilliseconds,
        }).WaitAsync(cancellationToken).ConfigureAwait(false);

        int count = await locator.CountAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (count != 1)
        {
            throw new InvalidOperationException($"El portal PAM no presentó un único control {description}.");
        }
    }

    private static async Task<bool> IsUniqueVisibleAsync(ILocator locator)
    {
        return await locator.CountAsync().ConfigureAwait(false) == 1 &&
            await locator.IsVisibleAsync().ConfigureAwait(false);
    }

    private static async Task<bool> IsAnyVisibleAsync(ILocator locator)
    {
        int count = await locator.CountAsync().ConfigureAwait(false);
        for (int index = 0; index < count; index++)
        {
            if (await locator.Nth(index).IsVisibleAsync().ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> IsCredentialRejectionVisibleAsync(ILocator alerts)
    {
        int count = await alerts.CountAsync().ConfigureAwait(false);
        for (int index = 0; index < count; index++)
        {
            ILocator alert = alerts.Nth(index);
            if (!await alert.IsVisibleAsync().ConfigureAwait(false))
            {
                continue;
            }

            string text = (await alert.InnerTextAsync().ConfigureAwait(false)).Trim();
            if (PamLoginAlertClassifier.IsCredentialRejection(text))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        CancellationToken cancellationToken)
    {
        long startedAt = Environment.TickCount64;
        while (Environment.TickCount64 - startedAt < PortalTimeout.TotalMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await condition().ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(ObservationInterval, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
