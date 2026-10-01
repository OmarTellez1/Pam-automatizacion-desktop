using PamAutomation.Core.Diagnostics;

namespace PamAutomation.Core.Interfaces.Diagnostics;

/// <summary>Receives detailed Pending diagnostics retained only for the current process.</summary>
public interface IPendingReconciliationDiagnosticSink
{
    /// <summary>Replaces the previous trace with a newly prepared execution.</summary>
    void Begin(PendingReconciliationDiagnostic diagnostic);

    /// <summary>Starts a read stage and clears a previous terminal error.</summary>
    void RecordStage(string stage);

    /// <summary>Stores the complete preflight baseline without discarding recovery history.</summary>
    void RecordBaseline(PendingSnapshotDiagnostic snapshot, int exactPendingDuplicateCount);

    /// <summary>Appends a successful or failed read attempt.</summary>
    void RecordRead(PendingReadAttemptDiagnostic read);

    /// <summary>Records the effective user decision for the prepared execution.</summary>
    void RecordDecision(string decision);

    /// <summary>Records how many submissions PAM confirmed.</summary>
    void RecordSubmittedCount(int submittedCount);
    /// <summary>Updates counts synchronously in the submission workflow, independently of UI progress.</summary>
    void RecordSubmissionCounts(DiagnosticAttemptSummary counts) { }

    /// <summary>Appends one complete comparison attempt.</summary>
    void RecordAttempt(PendingComparisonAttemptDiagnostic attempt);

    /// <summary>Appends one read-only recovery action without exposing portal values in normal logs.</summary>
    void RecordRecovery(PendingReadRecoveryDiagnostic recovery);

    /// <summary>Records the final stable Pending error code.</summary>
    void RecordFailure(string errorCode);
}

/// <summary>Provides a defensive point-in-time copy for an explicit diagnostic export.</summary>
public interface IPendingReconciliationDiagnosticSource
{
    /// <summary>Returns the latest in-memory trace, if one exists.</summary>
    PendingReconciliationDiagnostic? CaptureSnapshot();
}
