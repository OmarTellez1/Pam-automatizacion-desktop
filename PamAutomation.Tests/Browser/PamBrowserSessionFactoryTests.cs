using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamBrowserSessionFactoryTests
{
    [TestMethod]
    public async Task CreateAsync_ValidAdapters_UsesVisibleEdgeAndRestrictedTemporaryContext()
    {
        // Arrange
        var harness = new BrowserHarness();
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());

        // Act
        await using IPamBrowserSession session = await factory.CreateAsync();

        // Assert
        Assert.IsNotNull(harness.LaunchOptions);
        Assert.AreEqual("msedge", harness.LaunchOptions.Channel);
        Assert.IsFalse(harness.LaunchOptions.Headless);
        Assert.IsNotNull(harness.ContextOptions);
        Assert.IsTrue(harness.ContextOptions.IgnoreHttpSErrors);
        Assert.IsTrue(harness.ContextOptions.BlockServiceWorkers);
        Assert.IsNotNull(harness.RequestPolicy);
        Assert.IsFalse(harness.RequestPolicy("https://10.30.17.117/path"));
        Assert.IsTrue(harness.RequestPolicy("https://example.test/"));
    }

    [TestMethod]
    public void OriginPolicy_DeceptiveOrDifferentPortUris_AreNotAllowed()
    {
        // Arrange
        var policy = new PamOriginPolicy();

        // Act and assert
        Assert.IsTrue(policy.IsAllowed(new Uri("https://10.30.17.117/portal")));
        Assert.IsTrue(policy.IsAllowed(new Uri("https://10.30.17.117:443/portal")));
        Assert.IsFalse(policy.IsAllowed(new Uri("http://10.30.17.117/portal")));
        Assert.IsFalse(policy.IsAllowed(new Uri("https://10.30.17.117:444/portal")));
        Assert.IsFalse(policy.IsAllowed(new Uri("https://10.30.17.117@evil.test/portal")));
        Assert.IsFalse(policy.IsAllowed(new Uri("https://10.30.17.117.evil.test/portal")));
    }

    [TestMethod]
    public void OriginPolicy_ExternalHttpAndHttpsRequests_AreAborted()
    {
        // Arrange
        var policy = new PamOriginPolicy();

        // Act and assert
        Assert.IsFalse(policy.ShouldAbortRequest(new Uri("https://10.30.17.117/resource")));
        Assert.IsTrue(policy.ShouldAbortRequest(new Uri("https://outside.test/resource")));
        Assert.IsTrue(policy.ShouldAbortRequest(new Uri("http://outside.test/resource")));
        Assert.IsFalse(policy.ShouldAbortRequest(new Uri("file:///local/resource")));
        Assert.IsTrue(policy.ShouldAbortRequest("https://[invalid"));
    }

    [TestMethod]
    public async Task NavigateAsync_ExternalUri_RejectsBeforeCallingPage()
    {
        // Arrange
        var harness = new BrowserHarness();
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());
        await using IPamBrowserSession session = await factory.CreateAsync();

        // Act
        await Assert.ThrowsExactlyAsync<PamBrowserNavigationNotAllowedException>(
            () => session.NavigateAsync(new Uri("https://outside.test/")));

        // Assert
        Assert.AreEqual(0, harness.PageNavigateCalls);
    }

    [TestMethod]
    public async Task NavigateAsync_AllowedUri_ReturnsOnlyStatusAndHttpDateContract()
    {
        // Arrange
        var harness = new BrowserHarness();
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());
        await using IPamBrowserSession session = await factory.CreateAsync();
        Uri target = new("https://10.30.17.117/");

        // Act
        PamNavigationResponse response = await session.NavigateAsync(target);

        // Assert
        Assert.AreEqual(target, response.Uri);
        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual("Tue, 15 Nov 1994 08:12:31 GMT", response.HttpDateHeader);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.Zero, response.RoundTripTime);
    }

    [TestMethod]
    public async Task NavigateAsync_AllowedUri_MeasuresRoundTripWithMonotonicTime()
    {
        // Arrange
        var harness = new BrowserHarness();
        var timeProvider = new ControlledTimeProvider();
        harness.DuringNavigation = () => timeProvider.Advance(TimeSpan.FromMilliseconds(240));
        await using var session = new PamBrowserSession(
            harness, harness, harness, harness, new PamOriginPolicy(), timeProvider);

        // Act
        PamNavigationResponse response = await session.NavigateAsync(new Uri("https://10.30.17.117/"));

        // Assert
        Assert.AreEqual(TimeSpan.FromMilliseconds(240), response.RoundTripTime);
    }

    [TestMethod]
    public async Task CreateAsync_PageCreationFails_DisposesEveryCreatedResourceAndReturnsControlledError()
    {
        // Arrange
        var harness = new BrowserHarness { FailPageCreation = true };
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());

        // Act
        PamBrowserInitializationException exception = await Assert.ThrowsExactlyAsync<PamBrowserInitializationException>(
            () => factory.CreateAsync());

        // Assert
        Assert.AreEqual(PamBrowserInitializationErrorCode.SessionInitializationFailed, exception.Code);
        Assert.AreEqual("No fue posible inicializar la sesión del navegador PAM.", exception.Message);
        CollectionAssert.AreEqual(
            new[] { "context", "browser", "engine" },
            harness.DisposalOrder);
    }

    [TestMethod]
    public async Task CreateAsync_CancelledDuringPartialCreation_DisposesEveryCreatedResource()
    {
        // Arrange
        var harness = new BrowserHarness { CancelPageCreation = true };
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());

        // Act
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => factory.CreateAsync());

        // Assert
        CollectionAssert.AreEqual(
            new[] { "context", "browser", "engine" },
            harness.DisposalOrder);
    }

    [TestMethod]
    public async Task CreateAsync_EdgeLaunchFails_ReturnsEdgeLaunchCodeAndDisposesRuntime()
    {
        // Arrange
        var harness = new BrowserHarness { FailEdgeLaunch = true };
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());

        // Act
        PamBrowserInitializationException exception = await Assert.ThrowsExactlyAsync<PamBrowserInitializationException>(
            () => factory.CreateAsync());

        // Assert
        Assert.AreEqual(PamBrowserInitializationErrorCode.EdgeLaunchFailed, exception.Code);
        Assert.AreEqual("No fue posible iniciar Microsoft Edge para la sesión PAM.", exception.Message);
        CollectionAssert.AreEqual(new[] { "engine" }, harness.DisposalOrder);
    }

    [TestMethod]
    public async Task DisposeAsync_HealthySession_DisposesPageContextBrowserAndEngineInOrder()
    {
        // Arrange
        var harness = new BrowserHarness();
        var factory = new PamBrowserSessionFactory(harness, new PamOriginPolicy());
        IPamBrowserSession session = await factory.CreateAsync();

        // Act
        await session.DisposeAsync();

        // Assert
        CollectionAssert.AreEqual(
            new[] { "page", "context", "browser", "engine" },
            harness.DisposalOrder);
    }

    private sealed class BrowserHarness : IPlaywrightEngineFactory, IPlaywrightEngine, IManagedBrowser, IManagedBrowserContext, IManagedPage, IPamLoginPage
    {
        public List<string> DisposalOrder { get; } = [];
        public EdgeLaunchOptions? LaunchOptions { get; private set; }
        public PamBrowserContextOptions? ContextOptions { get; private set; }
        public Func<string, bool>? RequestPolicy { get; private set; }
        public bool FailPageCreation { get; init; }
        public bool CancelPageCreation { get; init; }
        public bool FailEdgeLaunch { get; init; }
        public int PageNavigateCalls { get; private set; }
        public Action? DuringNavigation { get; set; }

        public Task<IPlaywrightEngine> CreateAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IPlaywrightEngine>(this);

        public Task<IManagedBrowser> LaunchEdgeAsync(EdgeLaunchOptions options, CancellationToken cancellationToken)
        {
            if (FailEdgeLaunch)
            {
                throw new InvalidOperationException("Synthetic Edge launch failure.");
            }

            LaunchOptions = options;
            return Task.FromResult<IManagedBrowser>(this);
        }

        public Task<IManagedBrowserContext> NewContextAsync(PamBrowserContextOptions options, CancellationToken cancellationToken)
        {
            ContextOptions = options;
            return Task.FromResult<IManagedBrowserContext>(this);
        }

        public Task InstallRequestPolicyAsync(Func<string, bool> shouldAbortRequest, CancellationToken cancellationToken)
        {
            RequestPolicy = shouldAbortRequest;
            return Task.CompletedTask;
        }

        public Task<IManagedPage> NewPageAsync(CancellationToken cancellationToken)
        {
            if (CancelPageCreation)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (FailPageCreation)
            {
                throw new InvalidOperationException("Synthetic page creation failure.");
            }

            return Task.FromResult<IManagedPage>(this);
        }

        public Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken)
        {
            PageNavigateCalls++;
            DuringNavigation?.Invoke();
            return Task.FromResult(new PamNavigationResponse(uri, 200, "Tue, 15 Nov 1994 08:12:31 GMT", TimeSpan.Zero));
        }

        public Task<bool> SubmitCredentialsAsync(string username, string password, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> SubmitTotpAsync(string code, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public ValueTask DisposeAsync()
        {
            string resource = FailEdgeLaunch
                ? "engine"
                : FailPageCreation || CancelPageCreation
                ? DisposalOrder.Count switch
                {
                    0 => "context",
                    1 => "browser",
                    _ => "engine",
                }
                : DisposalOrder.Count switch
            {
                0 when LaunchOptions is null => "engine",
                0 => "page",
                1 => "context",
                2 => "browser",
                _ => "engine",
            };

            DisposalOrder.Add(resource);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
    }
}
