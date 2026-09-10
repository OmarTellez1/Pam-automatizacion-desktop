using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Browser;

/// <summary>Owns the page, context, browser, and Playwright runtime for one PAM session.</summary>
public sealed class PamBrowserSession : IPamBrowserSession
{
    private readonly IPlaywrightEngine engine;
    private readonly IManagedBrowser browser;
    private readonly IManagedBrowserContext context;
    private readonly IManagedPage page;
    private readonly PamOriginPolicy originPolicy;
    private readonly TimeProvider timeProvider;
    private bool disposed;

    /// <summary>Initializes a session with already-created browser resources.</summary>
    /// <param name="engine">Playwright runtime adapter.</param>
    /// <param name="browser">Launched browser adapter.</param>
    /// <param name="context">Temporary context adapter.</param>
    /// <param name="page">Page adapter.</param>
    /// <param name="originPolicy">Authorized origin policy.</param>
    internal PamBrowserSession(
        IPlaywrightEngine engine,
        IManagedBrowser browser,
        IManagedBrowserContext context,
        IManagedPage page,
        PamOriginPolicy originPolicy,
        TimeProvider? timeProvider = null)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.page = page ?? throw new ArgumentNullException(nameof(page));
        this.originPolicy = originPolicy ?? throw new ArgumentNullException(nameof(originPolicy));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the login-capable page owned by this session.</summary>
    internal IPamLoginPage LoginPage => page as IPamLoginPage
        ?? throw new InvalidOperationException("La página de la sesión PAM no admite autenticación.");

    /// <summary>Gets the read-only Favorites page owned by this session.</summary>
    internal IPamFavoritesPage FavoritesPage => page as IPamFavoritesPage
        ?? throw new InvalidOperationException("La página de la sesión PAM no admite Favorites.");

    /// <summary>Gets the safe request-test page owned by this session.</summary>
    internal IPamRequestTestPage RequestTestPage => page as IPamRequestTestPage
        ?? throw new InvalidOperationException("La página de la sesión PAM no admite el modo de prueba.");

    /// <summary>Gets the explicitly authorized submission page owned by this session.</summary>
    internal IPamRequestSubmissionPage RequestSubmissionPage => page as IPamRequestSubmissionPage
        ?? throw new InvalidOperationException("La página de la sesión PAM no admite solicitudes reales.");

    /// <summary>Gets the read-only Pending page owned by this session.</summary>
    internal IPamPendingPage PendingPage => page as IPamPendingPage
        ?? throw new InvalidOperationException("La página de la sesión PAM no admite leer Pending.");

    /// <inheritdoc />
    public async Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(uri);

        if (!originPolicy.IsAllowed(uri))
        {
            throw new PamBrowserNavigationNotAllowedException();
        }

        long startTimestamp = timeProvider.GetTimestamp();
        PamNavigationResponse response = await page.NavigateAsync(uri, cancellationToken).ConfigureAwait(false);
        return response with { RoundTripTime = timeProvider.GetElapsedTime(startTimestamp) };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Exception? firstFailure = null;
        await DisposeAndCaptureAsync(page, exception => firstFailure ??= exception).ConfigureAwait(false);
        await DisposeAndCaptureAsync(context, exception => firstFailure ??= exception).ConfigureAwait(false);
        await DisposeAndCaptureAsync(browser, exception => firstFailure ??= exception).ConfigureAwait(false);
        await DisposeAndCaptureAsync(engine, exception => firstFailure ??= exception).ConfigureAwait(false);

        if (firstFailure is not null)
        {
            throw new InvalidOperationException("No fue posible cerrar completamente la sesión PAM.", firstFailure);
        }
    }

    private static async Task DisposeAndCaptureAsync(IAsyncDisposable resource, Action<Exception> capture)
    {
        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            capture(exception);
        }
    }
}
