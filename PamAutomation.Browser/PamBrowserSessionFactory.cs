using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Browser;

/// <summary>Creates an isolated visible Edge session constrained to the PAM origin.</summary>
public sealed class PamBrowserSessionFactory : IPamBrowserSessionFactory
{
    private static readonly EdgeLaunchOptions EdgeOptions = new("msedge", Headless: false);
    private static readonly PamBrowserContextOptions ContextOptions = new(IgnoreHttpSErrors: true, BlockServiceWorkers: true);

    private readonly IPlaywrightEngineFactory engineFactory;
    private readonly PamOriginPolicy originPolicy;

    /// <summary>Initializes a session factory using the production Playwright adapter.</summary>
    public PamBrowserSessionFactory()
        : this(new PlaywrightEngineFactory(), new PamOriginPolicy())
    {
    }

    /// <summary>Initializes a session factory with testable browser adapters.</summary>
    /// <param name="engineFactory">Factory for the Playwright runtime adapter.</param>
    /// <param name="originPolicy">Policy for the authorized PAM origin.</param>
    internal PamBrowserSessionFactory(IPlaywrightEngineFactory engineFactory, PamOriginPolicy originPolicy)
    {
        this.engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        this.originPolicy = originPolicy ?? throw new ArgumentNullException(nameof(originPolicy));
    }

    /// <inheritdoc />
    public async Task<IPamBrowserSession> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IPlaywrightEngine? engine = null;
        IManagedBrowser? browser = null;
        IManagedBrowserContext? context = null;
        IManagedPage? page = null;

        try
        {
            engine = await engineFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            await DisposeCreatedAsync(page, context, browser, engine).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await DisposeCreatedAsync(page, context, browser, engine).ConfigureAwait(false);
            throw new PamBrowserInitializationException(PamBrowserInitializationErrorCode.SessionInitializationFailed);
        }

        try
        {
            browser = await engine.LaunchEdgeAsync(EdgeOptions, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            await DisposeCreatedAsync(page, context, browser, engine).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await DisposeCreatedAsync(page, context, browser, engine).ConfigureAwait(false);
            throw new PamBrowserInitializationException(PamBrowserInitializationErrorCode.EdgeLaunchFailed);
        }

        try
        {
            context = await browser.NewContextAsync(ContextOptions, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await context.InstallRequestPolicyAsync(originPolicy.ShouldAbortRequest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            page = await context.NewPageAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return new PamBrowserSession(engine, browser, context, page, originPolicy);
        }
        catch (OperationCanceledException)
        {
            await DisposeCreatedAsync(page, context, browser, engine).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await DisposeCreatedAsync(page, context, browser, engine).ConfigureAwait(false);
            throw new PamBrowserInitializationException(PamBrowserInitializationErrorCode.SessionInitializationFailed);
        }
    }

    private static async Task DisposeCreatedAsync(
        IManagedPage? page,
        IManagedBrowserContext? context,
        IManagedBrowser? browser,
        IPlaywrightEngine? engine)
    {
        await DisposeIgnoringFailuresAsync(page).ConfigureAwait(false);
        await DisposeIgnoringFailuresAsync(context).ConfigureAwait(false);
        await DisposeIgnoringFailuresAsync(browser).ConfigureAwait(false);
        await DisposeIgnoringFailuresAsync(engine).ConfigureAwait(false);
    }

    private static async Task DisposeIgnoringFailuresAsync(IAsyncDisposable? resource)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // The original launch or cancellation error is the actionable result.
        }
    }
}
