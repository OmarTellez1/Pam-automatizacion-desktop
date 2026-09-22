using PamAutomation.Browser;

namespace PamAutomation.Tests.Browser;

/// <summary>Keeps authorized integration checks visible without changing production launch policy.</summary>
internal sealed class VisiblePlaywrightEngineFactory : IPlaywrightEngineFactory
{
    public async Task<IPlaywrightEngine> CreateAsync(CancellationToken cancellationToken) =>
        new VisibleEngine(await new PlaywrightEngineFactory().CreateAsync(cancellationToken));

    private sealed class VisibleEngine(IPlaywrightEngine engine) : IPlaywrightEngine
    {
        public Task<IManagedBrowser> LaunchEdgeAsync(EdgeLaunchOptions options, CancellationToken cancellationToken) =>
            engine.LaunchEdgeAsync(options with { Headless = false }, cancellationToken);

        public ValueTask DisposeAsync() => engine.DisposeAsync();
    }
}
