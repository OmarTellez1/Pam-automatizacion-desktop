using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests.Core.Requests;

[TestClass]
[TestCategory("Unit")]
public sealed class PamRequestTestServiceTests
{
    private static readonly DateTimeOffset EcuadorNow =
        new(2026, 9, 9, 10, 0, 0, TimeSpan.FromHours(-5));

    [TestMethod]
    public async Task ExecuteAsync_ValidSettings_ValidatesEveryFormAndReportsZeroSubmissions()
    {
        var harness = new RequestTestHarness();

        PamRequestTestResult result = await harness.Service.ExecuteAsync(
            CreateCredentials(),
            CreateSettings(),
            EcuadorNow);

        Assert.AreEqual(2, result.FavoriteCount);
        Assert.AreEqual(2, result.ValidatedCount);
        Assert.AreEqual(0, result.SubmittedCount);
        Assert.HasCount(2, harness.RequestPortal.Accounts);
        Assert.AreSame(harness.Session, harness.FavoritesFactory.ReceivedSession);
        Assert.AreSame(harness.Session, harness.RequestFactory.ReceivedSession);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidSettings_DoesNotAuthenticateOrOpenPortal()
    {
        var harness = new RequestTestHarness();
        RequestSettings invalid = CreateSettings() with { RequestReason = " " };

        PamRequestTestException exception = await Assert.ThrowsExactlyAsync<PamRequestTestException>(
            () => harness.Service.ExecuteAsync(CreateCredentials(), invalid, EcuadorNow));

        Assert.AreEqual(PamRequestTestErrorCode.InvalidSettings, exception.ErrorCode);
        Assert.AreEqual(0, harness.AuthenticatedFactory.CreateCalls);
        Assert.AreEqual(0, harness.RequestFactory.CreateCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_FormFailure_StopsAndReturnsSafeError()
    {
        var harness = new RequestTestHarness
        {
            PortalFailureAtCall = 2,
        };

        PamRequestTestException exception = await Assert.ThrowsExactlyAsync<PamRequestTestException>(
            () => harness.Service.ExecuteAsync(CreateCredentials(), CreateSettings(), EcuadorNow));

        Assert.AreEqual(PamRequestTestErrorCode.PortalUnavailable, exception.ErrorCode);
        Assert.DoesNotContain("fixture-account", exception.Message);
        Assert.HasCount(1, harness.RequestPortal.Accounts);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public void Result_ToString_DoesNotContainAccountIdentityAndSubmissionIsInvariantlyZero()
    {
        var result = new PamRequestTestResult(3, 3);

        Assert.AreEqual(0, result.SubmittedCount);
        Assert.AreEqual("PAM request test: 3 validated, 0 submitted", result.ToString());
    }

    private static PamCredentials CreateCredentials() =>
        new("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP");

    private static RequestSettings CreateSettings() => new()
    {
        StartDate = new DateOnly(2026, 9, 10),
        StartTime = new TimeOnly(9, 30),
        SessionHours = 2,
        RequestReason = "Prueba segura de formulario",
    };

    private sealed class RequestTestHarness
    {
        private int? portalFailureAtCall;

        public RequestTestHarness()
        {
            Session = new FakeSession();
            AuthenticatedFactory = new FakeAuthenticatedSessionFactory(Session);
            FavoritesFactory = new FakeFavoritesPortalFactory(new FakeFavoritesPortal(
            [
                new PamFavoriteAccount("fixture-system-a", "fixture-app-a", "fixture-account-a"),
                new PamFavoriteAccount("fixture-system-b", "fixture-app-b", "fixture-account-b"),
            ]));
            FavoritesService = new PamFavoritesService(AuthenticatedFactory, FavoritesFactory);
            RequestPortal = new FakeRequestTestPortal(() => portalFailureAtCall);
            RequestFactory = new FakeRequestTestPortalFactory(RequestPortal);
            Service = new PamRequestTestService(
                AuthenticatedFactory,
                FavoritesService,
                RequestFactory,
                new RequestSettingsValidator());
        }

        public int? PortalFailureAtCall
        {
            get => portalFailureAtCall;
            init => portalFailureAtCall = value;
        }

        public FakeSession Session { get; }
        public FakeAuthenticatedSessionFactory AuthenticatedFactory { get; }
        public FakeFavoritesPortalFactory FavoritesFactory { get; }
        public PamFavoritesService FavoritesService { get; }
        public FakeRequestTestPortal RequestPortal { get; }
        public FakeRequestTestPortalFactory RequestFactory { get; }
        public PamRequestTestService Service { get; }
    }

    private sealed class FakeAuthenticatedSessionFactory(IPamBrowserSession session)
        : IPamAuthenticatedSessionFactory
    {
        public int CreateCalls { get; private set; }

        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(session);
        }
    }

    private sealed class FakeFavoritesPortalFactory(IPamFavoritesPortal portal)
        : IPamFavoritesPortalFactory
    {
        public IPamBrowserSession? ReceivedSession { get; private set; }

        public IPamFavoritesPortal Create(IPamBrowserSession session)
        {
            ReceivedSession = session;
            return portal;
        }
    }

    private sealed class FakeFavoritesPortal(IReadOnlyList<PamFavoriteAccount> accounts)
        : IPamFavoritesPortal
    {
        public Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(accounts);
    }

    private sealed class FakeRequestTestPortalFactory(IPamRequestTestPortal portal)
        : IPamRequestTestPortalFactory
    {
        public int CreateCalls { get; private set; }
        public IPamBrowserSession? ReceivedSession { get; private set; }

        public IPamRequestTestPortal Create(IPamBrowserSession session)
        {
            CreateCalls++;
            ReceivedSession = session;
            return portal;
        }
    }

    private sealed class FakeRequestTestPortal(Func<int?> failureAtCall) : IPamRequestTestPortal
    {
        private int calls;
        public List<PamFavoriteAccount> Accounts { get; } = [];

        public Task ValidateAndDiscardAsync(
            PamFavoriteAccount account,
            RequestSettings settings,
            CancellationToken cancellationToken)
        {
            calls++;
            if (failureAtCall() == calls)
            {
                throw new InvalidOperationException("Synthetic failure for fixture-account.");
            }

            Accounts.Add(account);
            return Task.CompletedTask;
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
