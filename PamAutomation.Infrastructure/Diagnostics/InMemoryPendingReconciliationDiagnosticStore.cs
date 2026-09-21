using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Interfaces.Diagnostics;

namespace PamAutomation.Infrastructure.Diagnostics;

/// <summary>Keeps the latest detailed Pending reconciliation only in process memory.</summary>
public sealed class InMemoryPendingReconciliationDiagnosticStore :
    IPendingReconciliationDiagnosticSink,
    IPendingReconciliationDiagnosticSource
{
    private readonly object sync = new();
    private PendingReconciliationDiagnostic? current;

    /// <inheritdoc />
    public void Begin(PendingReconciliationDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        lock (sync)
        {
            current = Clone(diagnostic);
        }
    }

    /// <inheritdoc />
    public void RecordDecision(string decision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decision);
        lock (sync)
        {
            if (current is not null) current = current with { Decision = decision.Trim() };
        }
    }

    /// <inheritdoc />
    public void RecordSubmittedCount(int submittedCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(submittedCount);
        lock (sync)
        {
            if (current is not null) current = current with { SubmittedCount = submittedCount };
        }
    }

    /// <inheritdoc />
    public void RecordAttempt(PendingComparisonAttemptDiagnostic attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        lock (sync)
        {
            if (current is null) return;
            current = current with { Attempts = [.. current.Attempts, Clone(attempt)] };
        }
    }

    /// <inheritdoc />
    public void RecordFailure(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        lock (sync)
        {
            if (current is not null) current = current with { FailureCode = errorCode.Trim() };
        }
    }

    /// <inheritdoc />
    public PendingReconciliationDiagnostic? CaptureSnapshot()
    {
        lock (sync)
        {
            return current is null ? null : Clone(current);
        }
    }

    private static PendingReconciliationDiagnostic Clone(PendingReconciliationDiagnostic diagnostic) =>
        diagnostic with
        {
            ExpectedRequests = diagnostic.ExpectedRequests.ToArray(),
            BeforeSubmission = Clone(diagnostic.BeforeSubmission),
            Attempts = diagnostic.Attempts.Select(Clone).ToArray(),
        };

    private static PendingSnapshotDiagnostic Clone(PendingSnapshotDiagnostic snapshot) =>
        snapshot with { Requests = snapshot.Requests.ToArray() };

    private static PendingComparisonAttemptDiagnostic Clone(PendingComparisonAttemptDiagnostic attempt) =>
        attempt with
        {
            Groups = attempt.Groups.ToArray(),
            Requests = attempt.Requests.ToArray(),
            NewRequests = attempt.NewRequests.ToArray(),
        };
}
