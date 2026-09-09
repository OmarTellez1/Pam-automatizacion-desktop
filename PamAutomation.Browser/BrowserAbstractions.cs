using PamAutomation.Core.Browser;

namespace PamAutomation.Browser;

/// <summary>Creates an adapter over a Playwright runtime.</summary>
internal interface IPlaywrightEngineFactory
{
    /// <summary>Creates the runtime adapter.</summary>
    /// <param name="cancellationToken">Cancels creation.</param>
    /// <returns>The created adapter.</returns>
    Task<IPlaywrightEngine> CreateAsync(CancellationToken cancellationToken);
}

/// <summary>Adapter for the Playwright runtime needed by the browser session.</summary>
internal interface IPlaywrightEngine : IAsyncDisposable
{
    /// <summary>Launches Microsoft Edge with the supplied settings.</summary>
    /// <param name="options">Required Edge launch settings.</param>
    /// <param name="cancellationToken">Cancels launch.</param>
    /// <returns>The launched browser adapter.</returns>
    Task<IManagedBrowser> LaunchEdgeAsync(EdgeLaunchOptions options, CancellationToken cancellationToken);
}

/// <summary>Adapter for a temporary Playwright browser.</summary>
internal interface IManagedBrowser : IAsyncDisposable
{
    /// <summary>Creates a non-persistent browser context.</summary>
    /// <param name="options">Context isolation and security settings.</param>
    /// <param name="cancellationToken">Cancels context creation.</param>
    /// <returns>The created context adapter.</returns>
    Task<IManagedBrowserContext> NewContextAsync(PamBrowserContextOptions options, CancellationToken cancellationToken);
}

/// <summary>Adapter for a temporary browser context.</summary>
internal interface IManagedBrowserContext : IAsyncDisposable
{
    /// <summary>Installs the request policy for this context.</summary>
    /// <param name="shouldAbortRequest">Returns whether a request URL must be aborted.</param>
    /// <param name="cancellationToken">Cancels route registration.</param>
    /// <returns>A task that completes once the policy is active.</returns>
    Task InstallRequestPolicyAsync(Func<string, bool> shouldAbortRequest, CancellationToken cancellationToken);

    /// <summary>Creates a page inside this isolated context.</summary>
    /// <param name="cancellationToken">Cancels page creation.</param>
    /// <returns>The created page adapter.</returns>
    Task<IManagedPage> NewPageAsync(CancellationToken cancellationToken);
}

/// <summary>Adapter for the single page owned by a PAM session.</summary>
internal interface IManagedPage : IAsyncDisposable
{
    /// <summary>Navigates the page.</summary>
    /// <param name="uri">Validated target URI.</param>
    /// <param name="cancellationToken">Cancels navigation.</param>
    /// <returns>Non-sensitive navigation response information.</returns>
    Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken);
}

/// <summary>Browser operations needed by the real PAM login adapter.</summary>
internal interface IPamLoginPage
{
    /// <summary>Submits the primary PAM credentials and waits for an explicit outcome.</summary>
    Task<bool> SubmitCredentialsAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>Submits one TOTP value and waits for acceptance or explicit rejection.</summary>
    Task<bool> SubmitTotpAsync(string code, CancellationToken cancellationToken);

    /// <summary>Waits until the previously visible TOTP rejection is no longer visible.</summary>
    Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken);

    /// <summary>Waits for the final dashboard confirmation link.</summary>
    Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken);
}

/// <summary>Required settings for launching the externally installed Edge channel.</summary>
/// <param name="Channel">The installed browser channel.</param>
/// <param name="Headless">Whether the browser is headless.</param>
internal sealed record EdgeLaunchOptions(string Channel, bool Headless);

/// <summary>Required settings for the temporary PAM browser context.</summary>
/// <param name="IgnoreHttpSErrors">Whether certificate errors are ignored in this context only.</param>
/// <param name="BlockServiceWorkers">Whether service workers are blocked so routing covers requests.</param>
internal sealed record PamBrowserContextOptions(bool IgnoreHttpSErrors, bool BlockServiceWorkers);
