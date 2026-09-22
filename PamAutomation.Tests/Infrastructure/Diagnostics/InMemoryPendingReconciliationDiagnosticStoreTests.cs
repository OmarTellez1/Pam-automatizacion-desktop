using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Infrastructure.Diagnostics;

namespace PamAutomation.Tests.Infrastructure.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class InMemoryPendingReconciliationDiagnosticStoreTests
{
    [TestMethod]
    public void RecordBaseline_AfterRecovery_PreservesHistoryAndCopiesBothDirections()
    {
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        store.Begin(new PendingReconciliationDiagnostic(3, "execution", DateTimeOffset.UtcNow,
            "ReadingPreflight", 0, [], null, 0, [], [], null) { Stage = "Preflight" });
        var read = new PendingReadAttemptDiagnostic(DateTimeOffset.UtcNow, "Preflight", 1, 0, 1, "PortalUnavailable");
        store.RecordRead(read);
        store.RecordRecovery(new PendingReadRecoveryDiagnostic(DateTimeOffset.UtcNow, 1, 0, 1,
            "ReloadPendingView", "PortalUnavailable") { Stage = "Preflight" });
        var baseline = new PendingRequestDiagnostic("100", "system", "application", "account",
            new DateOnly(2026, 9, 22), new TimeOnly(9, 0), new DateOnly(2026, 9, 22), new TimeOnly(10, 0), "reason");
        PendingRequestDiagnostic[] rows = [baseline];

        store.RecordBaseline(new PendingSnapshotDiagnostic(DateTimeOffset.UtcNow, 1, 1, rows), 1);
        rows[0] = baseline with { RequestId = "999" };
        PendingReconciliationDiagnostic snapshot = store.CaptureSnapshot()!;
        Assert.IsNotNull(snapshot.BeforeSubmission);
        Assert.AreEqual("100", Assert.ContainsSingle(snapshot.BeforeSubmission.Requests).RequestId);
        Assert.AreEqual(1, snapshot.ExactPendingDuplicateCount);
        Assert.AreEqual(read, Assert.ContainsSingle(snapshot.Reads));
        Assert.AreEqual("Preflight", Assert.ContainsSingle(snapshot.Recoveries).Stage);

        ((PendingRequestDiagnostic[])snapshot.BeforeSubmission.Requests)[0] = rows[0];
        ((PendingReadAttemptDiagnostic[])snapshot.Reads)[0] = read with { ErrorCode = "Changed" };
        store.RecordStage("Reconciliation");
        PendingReconciliationDiagnostic next = store.CaptureSnapshot()!;
        Assert.AreEqual("100", Assert.ContainsSingle(next.BeforeSubmission!.Requests).RequestId);
        Assert.AreEqual("PortalUnavailable", Assert.ContainsSingle(next.Reads).ErrorCode);
        Assert.AreEqual("Preflight", Assert.ContainsSingle(next.Reads).Stage);
        Assert.AreEqual("Reconciliation", next.Stage);
        Assert.IsNull(next.FailureCode);
    }

    [TestMethod]
    public void CaptureSnapshot_RecordedFailure_ReturnsDefensiveDetailedTrace()
    {
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var baseline = new PendingRequestDiagnostic(
            "100",
            "system",
            "application",
            "account",
            new DateOnly(2026, 9, 21),
            new TimeOnly(9, 0),
            new DateOnly(2026, 9, 21),
            new TimeOnly(10, 0),
            "reason");
        store.Begin(new PendingReconciliationDiagnostic(
            2,
            "execution",
            DateTimeOffset.UtcNow,
            "AwaitingExistingPendingDecision",
            1,
            [],
            new PendingSnapshotDiagnostic(DateTimeOffset.UtcNow, 1, 1, [baseline]),
            0,
            [],
            [],
            null));

        store.RecordDecision("UseExistingRequestIds");
        store.RecordSubmittedCount(1);
        store.RecordAttempt(new PendingComparisonAttemptDiagnostic(
            1,
            DateTimeOffset.UtcNow,
            2,
            2,
            1,
            0,
            1,
            1,
            0,
            1,
            0,
            null,
            [new PendingGroupMultiplicityDiagnostic("system", "account", 1, 1)],
            [baseline, baseline with { RequestId = "101" }],
            [baseline with { RequestId = "101" }]));
        store.RecordRecovery(new PendingReadRecoveryDiagnostic(
            DateTimeOffset.UtcNow,
            1,
            1,
            1,
            "ReloadPendingView",
            "PortalUnavailable"));
        store.RecordFailure("FieldMismatch");

        PendingReconciliationDiagnostic? snapshot = store.CaptureSnapshot();
        Assert.IsNotNull(snapshot);
        Assert.AreEqual("UseExistingRequestIds", snapshot.Decision);
        Assert.AreEqual(1, snapshot.SubmittedCount);
        Assert.AreEqual("FieldMismatch", snapshot.FailureCode);
        PendingComparisonAttemptDiagnostic attempt = Assert.ContainsSingle(snapshot.Attempts);
        Assert.AreEqual(1, attempt.NewRequestCount);
        Assert.AreEqual("101", Assert.ContainsSingle(attempt.NewRequests).RequestId);
        PendingReadRecoveryDiagnostic recovery = Assert.ContainsSingle(snapshot.Recoveries);
        Assert.AreEqual("ReloadPendingView", recovery.Action);
        Assert.DoesNotContain("execution", snapshot.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("100", baseline.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("system", Assert.ContainsSingle(attempt.Groups).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PortalUnavailable", recovery.ToString(), StringComparison.Ordinal);
    }
}
