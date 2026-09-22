using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Resilience;
using PamAutomation.Infrastructure.Diagnostics;

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
    public void Compare_DifferentApplication_UsesStablePendingFields()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("NOSQLBOOSTER", "NOSQL BOOSTER", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            "NOSQLBOOSTER",
            "Application Session",
            "account-a");

        PamPendingComparisonResult result = harness.Service.Compare([pending], [expected], CreateForm());

        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual("170001", result.RequestIds[0]);
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
    [DataRow("Acceso futuro 383", "Nombre completamente distinto en Pending")]
    public void Compare_ArbitraryPortalApplicationName_DoesNotAffectMatch(
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
    public void Compare_RepeatedSystemAndAccount_MatchesByCountWithoutApplicationFamilies()
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

        PamPendingComparisonResult result = harness.Service.Compare(requests, expected, CreateForm());

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { "170001", "170002" }, result.RequestIds.ToArray());
    }

    [TestMethod]
    public void Compare_SameAccountWithDifferentApplications_MatchesDeterministicallyByRequestId()
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
        CollectionAssert.AreEqual(new[] { "151904", "151905" }, result.RequestIds.ToArray());
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
    public void FindExactMatches_RepeatedPendingRows_ReturnsOnlyFullyEqualRequests()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP", "account-a");
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("170001", application: "Pending name one"),
            CreatePending("170002", application: "Pending name two"),
            CreatePending("170003", description: "Different reason"),
            CreatePending("170004", startTime: new TimeOnly(9, 5)),
        ];

        IReadOnlyList<PamPendingRequest> duplicates = harness.Service.FindExactMatches(
            requests,
            [expected],
            CreateForm());

        CollectionAssert.AreEqual(new[] { "170001", "170002" },
            duplicates.Select(static request => request.RequestId).ToArray());
    }

    [TestMethod]
    public void Compare_PreexistingExactRowExcluded_MatchesOnlyNewRepeatedRequest()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP", "account-a");
        IReadOnlyList<PamPendingRequest> requests =
        [
            CreatePending("170001"),
            CreatePending("170002"),
        ];

        PamPendingComparisonResult result = harness.Service.Compare(
            requests,
            [expected],
            CreateForm(),
            excludedRequestIds: ["170001"]);

        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual("170002", result.RequestIds[0]);
        Assert.DoesNotContain("170001", result.RequestIds);
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
    public void Compare_DifferentExpiration_IgnoresUnrelatedRowAndReportsMissing()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            application: "WinSCP - Nodo1",
            expiresTime: new TimeOnly(11, 1));

        PamPendingComparisonResult result = harness.Service.Compare([pending], [expected], CreateForm());

        Assert.IsFalse(result.IsComplete);
        Assert.AreEqual(1, result.MissingCount);
    }

    [TestMethod]
    public void Compare_DifferentDescription_IgnoresUnrelatedRowAndReportsMissing()
    {
        var harness = new PendingHarness([]);
        var expected = new PamFavoriteAccount("system-a", "WinSCP - Nodo1", "account-a");
        PamPendingRequest pending = CreatePending(
            "170001",
            application: "WinSCP - Nodo1",
            description: "Another reason");

        PamPendingComparisonResult result = harness.Service.Compare([pending], [expected], CreateForm());

        Assert.IsFalse(result.IsComplete);
        Assert.AreEqual(1, result.MissingCount);
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

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_BaselineRemainsVisible_ReturnsOnlyNewRequestId()
    {
        var expected = new PamFavoriteAccount("system-a", "Favorite application", "account-a");
        PamPendingRequest oldRequest = CreatePending("170001", application: "Old Pending application");
        PamPendingRequest newRequest = CreatePending("170002", application: "New unrelated Pending label");
        var portal = new SequencedPortal([[oldRequest], [oldRequest, newRequest]]);
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
            retryDelay: TimeSpan.FromMilliseconds(1),
            excludedRequestIds: ["170001"]);

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { "170002" }, result.RequestIds.ToArray());
        Assert.DoesNotContain("170001", result.RequestIds);
        Assert.AreEqual(2, portal.ReadCalls);
    }

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_AmbiguousNewRows_RecordsDetailedSetDifference()
    {
        PamPendingRequest oldRequest = CreatePending("170000");
        IReadOnlyList<PamPendingRequest> current =
        [
            oldRequest,
            CreatePending("170001"),
            CreatePending("170002"),
        ];
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        PendingRequestDiagnostic oldDiagnostic = PendingRequestDiagnostic.Create(oldRequest);
        store.Begin(new PendingReconciliationDiagnostic(
            1,
            "execution",
            DateTimeOffset.UtcNow,
            "Submit",
            1,
            [],
            new PendingSnapshotDiagnostic(DateTimeOffset.UtcNow, 1, 1, [oldDiagnostic]),
            1,
            [],
            [],
            null));
        var service = new PamPendingService(
            new FakeAuthenticatedSessionFactory(new FakeSession()),
            new FakePortalFactory(new FakePortal(current)),
            new FakeDelay(),
            store);

        PamPendingException exception = await Assert.ThrowsExactlyAsync<PamPendingException>(() =>
            service.WaitForCompleteComparisonAsync(
                new FakeSession(),
                [new PamFavoriteAccount("system-a", "WinSCP", "account-a")],
                CreateForm(),
                maximumAttempts: 1,
                excludedRequestIds: ["170000"]));

        Assert.AreEqual(PamPendingErrorCode.AmbiguousIdentity, exception.ErrorCode);
        PendingReconciliationDiagnostic? trace = store.CaptureSnapshot();
        Assert.IsNotNull(trace);
        PendingComparisonAttemptDiagnostic attempt = Assert.ContainsSingle(trace.Attempts);
        Assert.AreEqual(3, attempt.RowCount);
        Assert.AreEqual(3, attempt.UniqueRequestIdCount);
        Assert.AreEqual(1, attempt.BaselineIntersectionCount);
        Assert.AreEqual(0, attempt.MissingBaselineCount);
        Assert.AreEqual(2, attempt.NewRequestCount);
        Assert.AreEqual("AmbiguousIdentity", attempt.ErrorCode);
        Assert.HasCount(2, attempt.NewRequests);
    }

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_RepeatedStableKeyWaitsForFullNewCount()
    {
        IReadOnlyList<PamFavoriteAccount> expected =
        [
            new PamFavoriteAccount("system-a", "Favorite application A", "account-a"),
            new PamFavoriteAccount("system-a", "Favorite application B", "account-a"),
        ];
        PamPendingRequest oldRequest = CreatePending("170001", application: "Old label");
        PamPendingRequest firstNewRequest = CreatePending("170002", application: "Pending label X");
        PamPendingRequest secondNewRequest = CreatePending("170003", application: "Pending label Y");
        var portal = new SequencedPortal(
        [
            [oldRequest, firstNewRequest],
            [oldRequest, firstNewRequest, secondNewRequest],
        ]);
        var delay = new FakeDelay();
        var session = new FakeSession();
        var service = new PamPendingService(
            new FakeAuthenticatedSessionFactory(session),
            new FakePortalFactory(portal),
            delay);

        PamPendingComparisonResult result = await service.WaitForCompleteComparisonAsync(
            session,
            expected,
            CreateForm(),
            maximumAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(1),
            excludedRequestIds: ["170001"]);

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { "170002", "170003" }, result.RequestIds.ToArray());
        Assert.DoesNotContain("170001", result.RequestIds);
        Assert.AreEqual(2, portal.ReadCalls);
        Assert.AreEqual(1, delay.Calls);
    }

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_TransientReadTimeout_ReloadsPendingInSameSession()
    {
        var expected = new PamFavoriteAccount("system-a", "WinSCP", "account-a");
        var portal = new SequencedOutcomePortal(
            new TimeoutException("Transient grid timeout."),
            (IReadOnlyList<PamPendingRequest>)[CreatePending("170001")]);
        var delay = new FakeDelay();
        var session = new FakeSession();
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(
            new FakeAuthenticatedSessionFactory(session),
            new FakePortalFactory(portal),
            delay,
            store);

        PamPendingComparisonResult result = await service.WaitForCompleteComparisonAsync(
            session,
            [expected],
            CreateForm(),
            maximumAttempts: 1,
            retryDelay: TimeSpan.FromMilliseconds(1));

        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(2, portal.ReadCalls);
        Assert.AreEqual(1, delay.Calls);
        Assert.AreEqual(0, session.DisposeCalls);
        PendingReconciliationDiagnostic? trace = store.CaptureSnapshot();
        Assert.IsNotNull(trace);
        PendingReadRecoveryDiagnostic recovery = Assert.ContainsSingle(trace.Recoveries);
        Assert.AreEqual("ReloadPendingView", recovery.Action);
        Assert.IsNull(trace.FailureCode);
    }

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_StalledRead_RecreatesOnlyReadSessionAndPreservesBaseline()
    {
        var expected = new PamFavoriteAccount("system-a", "WinSCP", "account-a");
        PamPendingRequest baseline = CreatePending("170001");
        PamPendingRequest created = CreatePending("170002");
        var portal = new SequencedOutcomePortal(
            new TimeoutException("First read timeout."),
            new TimeoutException("Reload timeout."),
            (IReadOnlyList<PamPendingRequest>)[baseline, created]);
        var firstSession = new FakeSession();
        var recoveredSession = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(firstSession, recoveredSession);
        var delay = new FakeDelay();
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(
            sessions,
            new FakePortalFactory(portal),
            delay,
            store);

        PamPendingComparisonResult result = await service.WaitForCompleteComparisonAsync(
            CreateCredentials(),
            [expected],
            CreateForm(),
            maximumAttempts: 1,
            retryDelay: TimeSpan.FromMilliseconds(1),
            excludedRequestIds: [baseline.RequestId]);

        Assert.IsTrue(result.IsComplete);
        CollectionAssert.AreEqual(new[] { created.RequestId }, result.RequestIds.ToArray());
        Assert.AreEqual(2, sessions.CreateCalls);
        Assert.AreEqual(1, firstSession.DisposeCalls);
        Assert.AreEqual(1, recoveredSession.DisposeCalls);
        Assert.AreEqual(3, portal.ReadCalls);
        PendingReconciliationDiagnostic? trace = store.CaptureSnapshot();
        Assert.IsNotNull(trace);
        Assert.HasCount(3, trace.Recoveries);
        Assert.AreEqual("ReloadPendingView", trace.Recoveries[0].Action);
        Assert.AreEqual("ReadSessionFailed", trace.Recoveries[1].Action);
        Assert.AreEqual("RecreateAuthenticatedReadSession", trace.Recoveries[2].Action);
        Assert.IsNull(trace.FailureCode);
    }

    [TestMethod]
    public async Task WaitForCompleteComparisonAsync_PersistentReadTimeout_StopsAfterBoundedRecovery()
    {
        var portal = new SequencedOutcomePortal(new TimeoutException("Persistent timeout."));
        var firstSession = new FakeSession();
        var recoveredSession = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(firstSession, recoveredSession);
        var delay = new FakeDelay();
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(
            sessions,
            new FakePortalFactory(portal),
            delay,
            store);

        PamPendingException exception = await Assert.ThrowsExactlyAsync<PamPendingException>(() =>
            service.WaitForCompleteComparisonAsync(
                CreateCredentials(),
                [new PamFavoriteAccount("system-a", "WinSCP", "account-a")],
                CreateForm(),
                maximumAttempts: 1,
                retryDelay: TimeSpan.FromMilliseconds(1)));

        Assert.AreEqual(PamPendingErrorCode.PortalUnavailable, exception.ErrorCode);
        Assert.AreEqual(
            PamOperationTimingPolicy.PendingReadSessionMaximumAttempts,
            sessions.CreateCalls);
        Assert.AreEqual(
            PamOperationTimingPolicy.PendingReadSessionMaximumAttempts *
                PamOperationTimingPolicy.PendingReadMaximumAttemptsPerSession,
            portal.ReadCalls);
        Assert.AreEqual(1, firstSession.DisposeCalls);
        Assert.AreEqual(1, recoveredSession.DisposeCalls);
        PendingReconciliationDiagnostic? trace = store.CaptureSnapshot();
        Assert.IsNotNull(trace);
        Assert.AreEqual("PortalUnavailable", trace.FailureCode);
        Assert.HasCount(5, trace.Recoveries);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task ReadAllAsync_PreflightTimeouts_RecoversCorrectSnapshot(int failures)
    {
        var outcomes = Enumerable.Repeat<object>(new TimeoutException("Grid timeout"), failures)
            .Append((IReadOnlyList<PamPendingRequest>)[CreatePending("170001")]).ToArray();
        var portal = new SequencedOutcomePortal(outcomes);
        var first = new FakeSession();
        var second = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(first, second);
        var store = CreateDiagnosticStore();
        store.RecordFailure("PreviousFailure");
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay(), store);

        IReadOnlyList<PamPendingRequest> result = await service.ReadAllAsync(CreateCredentials());

        Assert.AreEqual("170001", Assert.ContainsSingle(result).RequestId);
        Assert.AreEqual(failures + 1, portal.ReadCalls);
        Assert.AreEqual(failures, sessions.CreateCalls);
        Assert.AreEqual(1, first.DisposeCalls);
        Assert.AreEqual(failures - 1, second.DisposeCalls);
        PendingReconciliationDiagnostic trace = store.CaptureSnapshot()!;
        Assert.AreEqual("Preflight", trace.Stage);
        Assert.IsNull(trace.FailureCode);
        Assert.HasCount(failures + 1, trace.Reads);
        Assert.IsTrue(trace.Reads.All(read => read.Stage == "Preflight" && read.ComparisonAttempt == 0));
        Assert.IsTrue(trace.Recoveries.All(recovery => recovery.Stage == "Preflight"));
        Assert.AreEqual("PortalUnavailable", trace.Reads[0].ErrorCode);
        Assert.AreEqual(1, trace.Reads[0].SessionAttempt);
        Assert.AreEqual(1, trace.Reads[0].ReadAttempt);
        Assert.IsNull(trace.Reads[^1].ErrorCode);
        Assert.AreEqual(failures, trace.Reads[^1].SessionAttempt);
    }

    [TestMethod]
    public async Task ReadAllAsync_PreflightPersistentTimeout_StopsAfterFourReads()
    {
        var portal = new SequencedOutcomePortal(new TimeoutException());
        var sessions = new SequencedAuthenticatedSessionFactory(new FakeSession(), new FakeSession());
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay(), store);

        PamPendingException failure = await Assert.ThrowsExactlyAsync<PamPendingException>(
            () => service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamPendingErrorCode.PortalUnavailable, failure.ErrorCode);
        Assert.AreEqual(4, portal.ReadCalls);
        Assert.AreEqual(2, sessions.CreateCalls);
        PendingReconciliationDiagnostic trace = store.CaptureSnapshot()!;
        Assert.AreEqual("PortalUnavailable", trace.FailureCode);
        Assert.HasCount(4, trace.Reads);
        Assert.HasCount(5, trace.Recoveries);
    }

    [TestMethod]
    [DataRow(PamPendingErrorCode.InvalidRequest)]
    [DataRow(PamPendingErrorCode.DuplicateRequestId)]
    [DataRow(PamPendingErrorCode.AmbiguousIdentity)]
    [DataRow(PamPendingErrorCode.FieldMismatch)]
    public async Task ReadAllAsync_PermanentFailure_DoesNotRetry(PamPendingErrorCode code)
    {
        var portal = new SequencedOutcomePortal(new PamPendingException(code));
        var session = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(session);
        var delay = new FakeDelay();
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), delay, store);

        PamPendingException failure = await Assert.ThrowsExactlyAsync<PamPendingException>(
            () => service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(code, failure.ErrorCode);
        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(1, portal.ReadCalls);
        Assert.AreEqual(1, session.DisposeCalls);
        Assert.AreEqual(0, delay.Calls);
        Assert.IsEmpty(store.CaptureSnapshot()!.Recoveries);
    }

    [TestMethod]
    public async Task ReadAllAsync_UnclassifiedFailure_DoesNotTreatItAsTransientNetworkError()
    {
        var portal = new SequencedOutcomePortal(new InvalidOperationException("Invalid structure"));
        var sessions = new SequencedAuthenticatedSessionFactory(new FakeSession());
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay());

        PamPendingException failure = await Assert.ThrowsExactlyAsync<PamPendingException>(
            () => service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(PamPendingErrorCode.InvalidRequest, failure.ErrorCode);
        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(1, portal.ReadCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_SnapshotUnstable_ReloadsWithoutAuthentication()
    {
        var portal = new SequencedOutcomePortal(new PamPendingException(PamPendingErrorCode.SnapshotUnstable),
            (IReadOnlyList<PamPendingRequest>)[CreatePending("170001")]);
        var sessions = new SequencedAuthenticatedSessionFactory(new FakeSession());
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay());

        IReadOnlyList<PamPendingRequest> result = await service.ReadAllAsync(CreateCredentials());

        Assert.HasCount(1, result);
        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(2, portal.ReadCalls);
    }

    [TestMethod]
    public async Task ReadAllAsync_CanceledRead_DoesNotRetryAndDisposesSession()
    {
        var portal = new SequencedOutcomePortal(new OperationCanceledException());
        var session = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(session);
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay(), store);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(1, portal.ReadCalls);
        Assert.AreEqual(1, session.DisposeCalls);
        Assert.AreEqual("Canceled", store.CaptureSnapshot()!.FailureCode);
        Assert.IsEmpty(store.CaptureSnapshot()!.Recoveries);
    }

    [TestMethod]
    public async Task ReadAllAsync_CanceledRecoveryDelay_DoesNotCreateAnotherSession()
    {
        var portal = new SequencedOutcomePortal(new TimeoutException());
        var session = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(session);
        using var cancellation = new CancellationTokenSource();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new CancelingDelay(cancellation));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.ReadAllAsync(CreateCredentials(), cancellation.Token));

        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(1, portal.ReadCalls);
        Assert.AreEqual(1, session.DisposeCalls);
    }

    private sealed class CancelingDelay(CancellationTokenSource cancellation) : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task ReadAllAsync_AuthenticationTimeout_DoesNotRetryLoginOrRead()
    {
        var sessions = new FailingAuthenticatedSessionFactory();
        var portal = new SequencedOutcomePortal((IReadOnlyList<PamPendingRequest>)[]);
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay(), store);

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(0, portal.ReadCalls);
        Assert.AreEqual("ReadSessionUnavailable", store.CaptureSnapshot()!.FailureCode);
        Assert.IsEmpty(store.CaptureSnapshot()!.Recoveries);
    }

    [TestMethod]
    public async Task ReadAllAsync_AlreadyCanceled_DoesNotAuthenticate()
    {
        var sessions = new SequencedAuthenticatedSessionFactory(new FakeSession());
        var portal = new SequencedOutcomePortal((IReadOnlyList<PamPendingRequest>)[]);
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay(), store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.ReadAllAsync(CreateCredentials(), cancellation.Token));

        Assert.AreEqual(0, sessions.CreateCalls);
        Assert.AreEqual(0, portal.ReadCalls);
        Assert.AreEqual("Canceled", store.CaptureSnapshot()!.FailureCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PendingRecovery_OverallDeadline_StopsWithoutNewSession(bool reconciliation)
    {
        var clock = new ManualDeadlineTimeProvider();
        var portal = new DeadlinePortal(clock);
        var session = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(session);
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay(), store, clock);

        PamPendingException failure = await Assert.ThrowsExactlyAsync<PamPendingException>(async () =>
        {
            if (reconciliation)
                await service.WaitForCompleteComparisonAsync(CreateCredentials(), [], CreateForm());
            else
                await service.ReadAllAsync(CreateCredentials());
        });

        Assert.AreEqual(PamPendingErrorCode.RecoveryTimeout, failure.ErrorCode);
        Assert.AreEqual(PamOperationTimingPolicy.PendingRecoveryTimeout, clock.DueTime);
        Assert.AreEqual(1, sessions.CreateCalls);
        Assert.AreEqual(1, session.DisposeCalls);
        Assert.AreEqual("RecoveryTimeout", store.CaptureSnapshot()!.FailureCode);
        Assert.IsEmpty(store.CaptureSnapshot()!.Recoveries);
    }

    [TestMethod]
    public async Task PendingRecovery_BrowserFailure_PreservesOperationAndCountsInDiagnostic()
    {
        var detail = new PendingBrowserReadDiagnostic("NextPage", 1, 2, 30, 25, false);
        var portal = new SequencedOutcomePortal(new PamPendingException(PamPendingErrorCode.InvalidRequest)
        {
            BrowserDiagnostic = detail,
        });
        var store = CreateDiagnosticStore();
        var service = new PamPendingService(new FakeAuthenticatedSessionFactory(new FakeSession()),
            new FakePortalFactory(portal), new FakeDelay(), store);

        await Assert.ThrowsExactlyAsync<PamPendingException>(() => service.ReadAllAsync(CreateCredentials()));

        Assert.AreEqual(detail, store.CaptureSnapshot()!.Reads.Single().Browser);
    }

    private sealed class DeadlinePortal(ManualDeadlineTimeProvider clock) : IPamPendingPortal
    {
        public Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(CancellationToken cancellationToken)
        {
            clock.Fire();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PamPendingRequest>>([]);
        }
    }

    [TestMethod]
    public async Task PendingRecovery_ExpiredPage_RecreatesSessionWithoutReadingClosedPage()
    {
        var expired = new PamPendingException(PamPendingErrorCode.SnapshotUnstable) { RequiresNewSession = true };
        var portal = new SequencedOutcomePortal(expired, (IReadOnlyList<PamPendingRequest>)[CreatePending("170001")]);
        var first = new FakeSession();
        var second = new FakeSession();
        var sessions = new SequencedAuthenticatedSessionFactory(first, second);
        var service = new PamPendingService(sessions, new FakePortalFactory(portal), new FakeDelay());

        IReadOnlyList<PamPendingRequest> result = await service.ReadAllAsync(CreateCredentials());

        Assert.HasCount(1, result);
        Assert.AreEqual(2, portal.ReadCalls);
        Assert.AreEqual(2, sessions.CreateCalls);
        Assert.AreEqual(1, first.DisposeCalls);
        Assert.AreEqual(1, second.DisposeCalls);
    }

    private sealed class ManualDeadlineTimeProvider : TimeProvider
    {
        private Action? callback;
        public TimeSpan DueTime { get; private set; }
        public override ITimer CreateTimer(TimerCallback timerCallback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            callback = () => timerCallback(state);
            return new ManualTimer();
        }
        public void Fire() => callback!();
        private sealed class ManualTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FailingAuthenticatedSessionFactory : IPamAuthenticatedSessionFactory
    {
        public int CreateCalls { get; private set; }
        public Task<IPamBrowserSession> CreateAuthenticatedAsync(PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromException<IPamBrowserSession>(new TimeoutException());
        }
    }

    private static InMemoryPendingReconciliationDiagnosticStore CreateDiagnosticStore()
    {
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        store.Begin(new PendingReconciliationDiagnostic(
            2,
            "execution",
            DateTimeOffset.UtcNow,
            "Submit",
            0,
            [],
            new PendingSnapshotDiagnostic(DateTimeOffset.UtcNow, 0, 0, []),
            1,
            [],
            [],
            null));
        return store;
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

    private sealed class SequencedAuthenticatedSessionFactory(params IPamBrowserSession[] sessions)
        : IPamAuthenticatedSessionFactory
    {
        public int CreateCalls { get; private set; }

        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            int index = Math.Min(CreateCalls, sessions.Length - 1);
            CreateCalls++;
            return Task.FromResult(sessions[index]);
        }
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

    private sealed class SequencedOutcomePortal(params object[] outcomes) : IPamPendingPortal
    {
        private int index;
        public int ReadCalls { get; private set; }

        public Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(CancellationToken cancellationToken)
        {
            ReadCalls++;
            object outcome = outcomes[Math.Min(index++, outcomes.Length - 1)];
            return outcome switch
            {
                Exception exception => Task.FromException<IReadOnlyList<PamPendingRequest>>(exception),
                IReadOnlyList<PamPendingRequest> requests => Task.FromResult(requests),
                _ => throw new InvalidOperationException("Unsupported Pending test outcome."),
            };
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
