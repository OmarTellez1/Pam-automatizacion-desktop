using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;

namespace PamAutomation.Tests.Core.Requests;

[TestClass]
[TestCategory("Unit")]
public sealed class PamPendingServiceTests
{
    [TestMethod]
    public async Task ReadAllAsync_ValidSnapshot_ReturnsDefensiveCopyAndDisposesSession()
    {
        var requests = new List<PamPendingRequest> { CreatePending("170001") };
        var harness = new PendingHarness(requests);

        IReadOnlyList<PamPendingRequest> result = await harness.Service.ReadAllAsync(CreateCredentials());
        requests.Clear();

        Assert.HasCount(1, result);
        Assert.AreSame(harness.Session, harness.PortalFactory.ReceivedSession);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_EmptyPending_IsValid()
    {
        var harness = new PendingHarness([]);

        IReadOnlyList<PamPendingRequest> result = await harness.Service.ReadAllAsync(CreateCredentials());

        Assert.IsEmpty(result);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_ExistingSession_DoesNotDisposeCallerSession()
    {
        var harness = new PendingHarness([CreatePending("170001")]);

        IReadOnlyList<PamPendingRequest> result = await harness.Service.ReadAllAsync(harness.Session);

        Assert.HasCount(1, result);
        Assert.AreEqual(0, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_DuplicateRequestId_ThrowsStableFailure()
    {
        var harness = new PendingHarness([CreatePending("170001"), CreatePending("170001")]);

        PamPendingException exception = await Assert.ThrowsExactlyAsync<PamPendingException>(
            () => harness.Service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamPendingErrorCode.DuplicateRequestId, exception.ErrorCode);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public void Compare_ExactStartAndIdentity_ReturnsOrderedMatches()
    {
        var harness = new PendingHarness([]);
        IReadOnlyList<PamFavoriteAccount> accounts =
        [
            new PamFavoriteAccount("SYSTEM-A", "WinSCP - Nodo1", "account-a"),
            new PamFavoriteAccount("system-b", "PuTTY - Nodo2", "account-b"),
        ];
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("170002", "system-b", " putty - nodo2 ", "account-b"),
            CreatePending("170001", "system-a", "winscp - nodo1", "account-a"),
            CreatePending("170003", "system-a", "WinSCP - Nodo1", "account-a", startTime: new TimeOnly(9, 5)),
        ];

        PamPendingComparisonResult result = harness.Service.Compare(requests, accounts, CreateForm());

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { "170001", "170002" }, result.RequestIds.ToArray());
        Assert.AreEqual("PAM Pending comparison: 2 matched, 0 missing", result.ToString());
    }

    [TestMethod]
    public void Compare_MissingExpectedAccount_ReturnsMissingWithoutGuessing()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-missing", "application", "account");

        PamPendingComparisonResult result = harness.Service.Compare([], [expected], CreateForm());

        Assert.IsFalse(result.IsComplete);
        Assert.AreEqual(0, result.MatchedCount);
        Assert.AreEqual(1, result.MissingCount);
        Assert.AreSame(expected, result.MissingAccounts[0]);
    }

    [TestMethod]
    public void Compare_DifferentApplication_DoesNotGuessIdentity()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("NOSQLBOOSTER", "NOSQL BOOSTER", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            "NOSQLBOOSTER",
            "Application Session",
            "account-a");

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => harness.Service.Compare([pending], [expected], CreateForm()));

        Assert.AreEqual(PamPendingErrorCode.FieldMismatch, exception.ErrorCode);
    }

    [TestMethod]
    public void Compare_ApplicationPortalSuffix_MatchesRequestedApplication()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("NOSQLBOOSTER", "NOSQLBOOSTER", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            "NOSQLBOOSTER",
            "NOSQLBOOSTER - PGDCPAMSER01",
            "account-a");

        PamPendingComparisonResult result = harness.Service.Compare([pending], [expected], CreateForm());

        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual("170001", result.RequestIds[0]);
    }

    [TestMethod]
    [DataRow("NOSQLBOOSTER", "NOSQLBOOSTER - PGDCPAMSER01")]
    [DataRow("PLSQL - Nodo1", "PLSQL - BDMOVIL-PGDC01")]
    [DataRow("PuTTY - Nodo2", "PuTTY 0.77 - PGDCMSA01")]
    [DataRow("WinSCP - nodo1", "WinSCP 5.21 N2 - PGDCMSA02")]
    public void Compare_ObservedPortalApplicationFamily_MatchesFavoriteName(
        string favoriteApplication,
        string pendingApplication)
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", favoriteApplication, "account-a");
        PamPendingRequest pending = CreatePending("170001", application: pendingApplication);

        PamPendingComparisonResult result = harness.Service.Compare([pending], [expected], CreateForm());

        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual("170001", result.RequestIds[0]);
    }

    [TestMethod]
    public void Compare_SameKnownFamilyTwiceForSameAccount_RejectsAmbiguity()
    {
        var harness = new PendingHarness([]);
        IReadOnlyList<PamFavoriteAccount> expected =
        [
            new PamFavoriteAccount("system-a", "PuTTY - Nodo1", "account-a"),
            new PamFavoriteAccount("system-a", "PuTTY - Nodo2", "account-a"),
        ];
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("170001", application: "PuTTY 0.77 - SERVER01"),
            CreatePending("170002", application: "PuTTY 0.77 - SERVER02"),
        ];

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => harness.Service.Compare(requests, expected, CreateForm()));

        Assert.AreEqual(PamPendingErrorCode.AmbiguousIdentity, exception.ErrorCode);
    }

    [TestMethod]
    public void Compare_SameAccountWithObservedPuttyAndWinscpNames_MatchesByFamily()
    {
        var harness = new PendingHarness([]);
        IReadOnlyList<PamFavoriteAccount> expected =
        [
            new PamFavoriteAccount("pgdcmsa02", "WinSCP - nodo1", "creditengineIg"),
            new PamFavoriteAccount("pgdcmsa02", "PuTTY - Nodo2", "creditengineIg"),
        ];
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("151904", "pgdcmsa02", "PuTTY 0.77 - PGDCMSA02", "creditengineIg"),
            CreatePending("151905", "pgdcmsa02", "WinSCP 5.21 N2 - PGDCMSA02", "creditengineIg"),
        ];

        PamPendingComparisonResult result = harness.Service.Compare(requests, expected, CreateForm());

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { "151905", "151904" }, result.RequestIds.ToArray());
    }

    [TestMethod]
    public void Compare_SameAccountWithDifferentApplications_MatchesEachApplication()
    {
        var harness = new PendingHarness([]);
        IReadOnlyList<PamFavoriteAccount> expected =
        [
            new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a"),
            new PamFavoriteAccount("system-a", "PuTTY - Nodo2", "account-a"),
        ];
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("170001", application: "WinSCP - Nodo1"),
            CreatePending("170002", application: "PuTTY - Nodo2"),
        ];

        PamPendingComparisonResult result = harness.Service.Compare(requests, expected, CreateForm());

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { "170001", "170002" }, result.RequestIds.ToArray());
    }

    [TestMethod]
    public void Compare_TwoPendingRowsForSameIdentityAndStart_RejectsAmbiguity()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a");
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("170001", "system-a", "WinSCP - Nodo1", "account-a"),
            CreatePending("170002", "system-a", "WinSCP - Nodo1", "account-a"),
        ];

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => harness.Service.Compare(requests, [expected], CreateForm()));

        Assert.AreEqual(PamPendingErrorCode.AmbiguousIdentity, exception.ErrorCode);
    }

    [TestMethod]
    public void Compare_DuplicateNormalizedExpectedIdentity_RejectsAmbiguity()
    {
        var harness = new PendingHarness([]);
        IReadOnlyList<PamFavoriteAccount> expected =
        [
            new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a"),
            new PamFavoriteAccount(" SYSTEM-A ", " winscp - nodo1 ", " ACCOUNT-A "),
        ];

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => harness.Service.Compare([], expected, CreateForm()));

        Assert.AreEqual(PamPendingErrorCode.AmbiguousIdentity, exception.ErrorCode);
    }

    [TestMethod]
    public void Compare_DifferentExpiration_RejectsFieldMismatch()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            application: "WinSCP - Nodo1",
            expiresTime: new TimeOnly(11, 1));

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => harness.Service.Compare([pending], [expected], CreateForm()));

        Assert.AreEqual(PamPendingErrorCode.FieldMismatch, exception.ErrorCode);
    }

    [TestMethod]
    public void Compare_DifferentDescription_RejectsFieldMismatch()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            application: "WinSCP - Nodo1",
            description: "Another reason");

        PamPendingException exception = Assert.ThrowsExactly<PamPendingException>(
            () => harness.Service.Compare([pending], [expected], CreateForm()));

        Assert.AreEqual(PamPendingErrorCode.FieldMismatch, exception.ErrorCode);
    }

    [TestMethod]
    public void PendingRequest_InvalidId_IsRejectedAndTextIsRedacted()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreatePending("REQ-1"));

        PamPendingRequest request = CreatePending("170001");
        Assert.AreEqual("PAM Pending request (redacted)", request.ToString());
        Assert.DoesNotContain(request.RequestId, request.ToString());
    }

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_EventualRow_RetriesUntilComplete()
    {
        var expected = new PamFavoriteAccount("system-a", "WinSCP", "account-a");
        var portal = new SequencedPortal([[], [CreatePending("170001")]]);
        var delay = new FakeDelay();
        var session = new FakeSession();
        var service = new PamPendingService(
            new FakeAuthenticatedSessionFactory(session),
            new FakePortalFactory(portal),
            delay);

        PamPendingComparisonResult result = await service.WaitForCompleteComparisonAsync(
            session,
            [expected],
            CreateForm(),
            maximumAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(1));

        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(2, portal.ReadCalls);
        Assert.AreEqual(1, delay.Calls);
    }

    private static PamRequestFormData CreateForm() => new(
        new DateOnly(2026, 9, 10),
        new TimeOnly(9, 0),
        0,
        2,
        0,
        "Authorized reason");

    private static PamPendingRequest CreatePending(
        string requestId,
        string system = "system-a",
        string application = "WinSCP",
        string account = "account-a",
        TimeOnly? startTime = null,
        DateOnly? expiresDate = null,
        TimeOnly? expiresTime = null,
        string description = "Authorized reason") => new(
            requestId,
            system,
            application,
            account,
            new DateOnly(2026, 9, 10),
            startTime ?? new TimeOnly(9, 0),
            expiresDate ?? new DateOnly(2026, 9, 10),
            expiresTime ?? new TimeOnly(11, 0),
            "Pending",
            description);

    private static PamCredentials CreateCredentials() =>
        new("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP");

    private sealed class PendingHarness
    {
        public PendingHarness(IReadOnlyList<PamPendingRequest> requests)
        {
            Session = new FakeSession();
            PortalFactory = new FakePortalFactory(new FakePortal(requests));
            Service = new PamPendingService(
                new FakeAuthenticatedSessionFactory(Session),
                PortalFactory,
                new FakeDelay());
        }

        public FakeSession Session { get; }
        public FakePortalFactory PortalFactory { get; }
        public PamPendingService Service { get; }
    }

    private sealed class FakeAuthenticatedSessionFactory(IPamBrowserSession session)
        : IPamAuthenticatedSessionFactory
    {
        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default) => Task.FromResult(session);
    }

    private sealed class FakePortalFactory(IPamPendingPortal portal) : IPamPendingPortalFactory
    {
        public IPamBrowserSession? ReceivedSession { get; private set; }

        public IPamPendingPortal Create(IPamBrowserSession session)
        {
            ReceivedSession = session;
            return portal;
        }
    }

    private sealed class FakePortal(IReadOnlyList<PamPendingRequest> requests) : IPamPendingPortal
    {
        public Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(requests);
    }

    private sealed class SequencedPortal(IReadOnlyList<IReadOnlyList<PamPendingRequest>> snapshots)
        : IPamPendingPortal
    {
        private int index;
        public int ReadCalls { get; private set; }

        public Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(CancellationToken cancellationToken)
        {
            ReadCalls++;
            int current = Math.Min(index++, snapshots.Count - 1);
            return Task.FromResult(snapshots[current]);
        }
    }

    private sealed class FakeDelay : IAsyncDelay
    {
        public int Calls { get; private set; }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Calls++;
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
