using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Tests.Core.Favorites;

[TestClass]
[TestCategory("Unit")]
public sealed class PamFavoritesServiceTests
{
    [TestMethod]
    public async Task ReadAllAsync_ValidUniqueAccounts_ReturnsSnapshotAndDisposesSession()
    {
        var harness = new FavoritesHarness(
        [
            new PamFavoriteAccount("system-a", "application-a", "account-a"),
            new PamFavoriteAccount("system-b", "application-b", "account-b"),
        ]);

        IReadOnlyList<PamFavoriteAccount> result = await harness.Service.ReadAllAsync(CreateCredentials());

        Assert.HasCount(2, result);
        Assert.AreSame(harness.Session, harness.PortalFactory.ReceivedSession);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_EmptyFavorites_ThrowsStableErrorAndDisposesSession()
    {
        var harness = new FavoritesHarness([]);

        PamFavoritesException exception = await Assert.ThrowsExactlyAsync<PamFavoritesException>(
            () => harness.Service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamFavoritesErrorCode.NoAccounts, exception.ErrorCode);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_DuplicateIdentity_ThrowsStableError()
    {
        var account = new PamFavoriteAccount("system", "application", "account");
        var harness = new FavoritesHarness([account, account]);

        PamFavoritesException exception = await Assert.ThrowsExactlyAsync<PamFavoritesException>(
            () => harness.Service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamFavoritesErrorCode.DuplicateAccount, exception.ErrorCode);
    }

    [TestMethod]
    public async Task ReadAllAsync_NullAccount_ThrowsStableError()
    {
        var harness = new FavoritesHarness([null!]);

        PamFavoritesException exception = await Assert.ThrowsExactlyAsync<PamFavoritesException>(
            () => harness.Service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamFavoritesErrorCode.InvalidAccount, exception.ErrorCode);
    }

    [TestMethod]
    public async Task ReadAllAsync_PortalFailure_ReturnsSafeErrorWithoutAccountValues()
    {
        var harness = new FavoritesHarness([])
        {
            PortalException = new InvalidOperationException("Synthetic portal failure."),
        };

        PamFavoritesException exception = await Assert.ThrowsExactlyAsync<PamFavoritesException>(
            () => harness.Service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamFavoritesErrorCode.PortalUnavailable, exception.ErrorCode);
        Assert.DoesNotContain("fixture", exception.Message);
    }

    [TestMethod]
    public void FavoriteAccount_IncompleteIdentity_IsRejectedAndTextIsRedacted()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PamFavoriteAccount("", "application", "account"));

        var account = new PamFavoriteAccount("system", "application", "account");
        Assert.AreEqual("PAM favorite account (redacted)", account.ToString());
    }

    private static PamCredentials CreateCredentials() =>
        new("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP");

    private sealed class FavoritesHarness
    {
        public FavoritesHarness(IReadOnlyList<PamFavoriteAccount> accounts)
        {
            Session = new FakeSession();
            AuthenticatedSessionFactory = new FakeAuthenticatedSessionFactory(Session);
            Portal = new FakePortal(accounts, () => PortalException);
            PortalFactory = new FakePortalFactory(Portal);
            Service = new PamFavoritesService(AuthenticatedSessionFactory, PortalFactory);
        }

        public Exception? PortalException { get; init; }
        public FakeSession Session { get; }
        public FakeAuthenticatedSessionFactory AuthenticatedSessionFactory { get; }
        public FakePortal Portal { get; }
        public FakePortalFactory PortalFactory { get; }
        public PamFavoritesService Service { get; }
    }

    private sealed class FakeAuthenticatedSessionFactory(IPamBrowserSession session)
        : IPamAuthenticatedSessionFactory
    {
        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default) => Task.FromResult(session);
    }

    private sealed class FakePortalFactory(IPamFavoritesPortal portal) : IPamFavoritesPortalFactory
    {
        public IPamBrowserSession? ReceivedSession { get; private set; }

        public IPamFavoritesPortal Create(IPamBrowserSession session)
        {
            ReceivedSession = session;
            return portal;
        }
    }

    private sealed class FakePortal(
        IReadOnlyList<PamFavoriteAccount> accounts,
        Func<Exception?> exception) : IPamFavoritesPortal
    {
        public Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return exception() is { } failure
                ? Task.FromException<IReadOnlyList<PamFavoriteAccount>>(failure)
                : Task.FromResult(accounts);
        }
    }

    private sealed class FakeSession : IPamBrowserSession
    {
        public int DisposeCalls { get; private set; }

        public Task<PamNavigationResponse> NavigateAsync(
            Uri uri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
