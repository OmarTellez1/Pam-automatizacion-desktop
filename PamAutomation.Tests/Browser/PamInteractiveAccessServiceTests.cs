using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamInteractiveAccessServiceTests
{
    [TestMethod]
    public async Task OpenAsync_AuthenticatedSession_DetachesWithoutClosingBrowserResources()
    {
        var session = new FakeDetachableSession();
        var authentication = new FakeAuthentication(session);
        var service = new PamInteractiveAccessService(authentication);

        await service.OpenAsync(new PamCredentials("user", "password", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"));

        Assert.AreEqual(1, authentication.CreateCount);
        Assert.AreEqual(1, session.DetachCount);
        Assert.AreEqual(0, session.DisposeCount);
    }

    [TestMethod]
    public async Task OpenAsync_NonDetachableSession_ClosesItAndFailsSafely()
    {
        var session = new FakeRegularSession();
        var service = new PamInteractiveAccessService(new FakeAuthentication(session));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.OpenAsync(new PamCredentials("user", "password", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ")));

        Assert.Contains("no se puede liberar", exception.Message);
        Assert.AreEqual(1, session.DisposeCount);
    }

    private sealed class FakeAuthentication(IPamBrowserSession session) : IPamAuthenticatedSessionFactory
    {
        public int CreateCount { get; private set; }

        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return Task.FromResult(session);
        }
    }

    private sealed class FakeDetachableSession : IDetachablePamBrowserSession
    {
        public int DetachCount { get; private set; }
        public int DisposeCount { get; private set; }

        public Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PamNavigationResponse(uri, 200, null, TimeSpan.Zero));

        public ValueTask DetachAsync()
        {
            DetachCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRegularSession : IPamBrowserSession
    {
        public int DisposeCount { get; private set; }

        public Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PamNavigationResponse(uri, 200, null, TimeSpan.Zero));

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
