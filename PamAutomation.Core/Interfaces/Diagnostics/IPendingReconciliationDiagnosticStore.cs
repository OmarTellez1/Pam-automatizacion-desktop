using PamAutomation.Core.Diagnostics;

namespace PamAutomation.Core.Interfaces.Diagnostics;

/// <summary>Receives detailed Pending diagnostics retained only for the current process.</summary>
public interface IPendingReconciliationDiagnosticSink
{
    /// <summary>Replaces the previous trace with a newly prepared execution.</summary>
    void Begin(PendingReconciliationDiagnostic diagnostic);

    /// <summary>Records the effective user decision for the prepared execution.</summary>
    void RecordDecision(string decision);

    /// <summary>Records how many submissions PAM confirmed.</summary>
    void RecordSubmittedCount(int submittedCount);

    /// <summary>Appends one complete comparison attempt.</summary>
    void RecordAttempt(PendingComparisonAttemptDiagnostic attempt);

    /// <summary>Records the final stable Pending error code.</summary>
    void RecordFailure(string errorCode);
}

/// <summary>Provides a defensive point-in-time copy for an explicit diagnostic export.</summary>
public interface IPendingReconciliationDiagnosticSource
{
    /// <summary>Returns the latest in-memory trace, if one exists.</summary>
    PendingReconciliationDiagnostic? CaptureSnapshot();
}
