using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Infrastructure.Diagnostics;

namespace PamAutomation.Tests.Infrastructure.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class InMemoryPendingReconciliationDiagnosticStoreTests
{
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
            1,
            "execution",
            DateTimeOffset.UtcNow,
            "AwaitingRepeatedPendingDecision",
            1,
            [],
            new PendingSnapshotDiagnostic(DateTimeOffset.UtcNow, 1, 1, [baseline]),
            0,
            [],
            null));

        store.RecordDecision("SubmitAgain");
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
        store.RecordFailure("FieldMismatch");

        PendingReconciliationDiagnostic? snapshot = store.CaptureSnapshot();
        Assert.IsNotNull(snapshot);
        Assert.AreEqual("SubmitAgain", snapshot.Decision);
        Assert.AreEqual(1, snapshot.SubmittedCount);
        Assert.AreEqual("FieldMismatch", snapshot.FailureCode);
        PendingComparisonAttemptDiagnostic attempt = Assert.ContainsSingle(snapshot.Attempts);
        Assert.AreEqual(1, attempt.NewRequestCount);
        Assert.AreEqual("101", Assert.ContainsSingle(attempt.NewRequests).RequestId);
        Assert.DoesNotContain("execution", snapshot.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("100", baseline.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("system", Assert.ContainsSingle(attempt.Groups).ToString(), StringComparison.Ordinal);
    }
}
