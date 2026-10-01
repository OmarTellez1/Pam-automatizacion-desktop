using Microsoft.Extensions.Logging;

namespace PamAutomation.Core.Diagnostics;

/// <summary>Safe snapshot of an attempt, independently of the last successful execution.</summary>
public sealed record DiagnosticAttemptSummary(Guid AttemptId, DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc, string Status, string Stage, int ExpectedCount,
    int FormOrdinal, int AcceptedCount, int ConflictCount, int RejectedCount,
    int UncertainCount, int NotAttemptedCount, int RequestIdCount)
{
    public int FailedBeforeSubmissionCount { get; init; }
    public string? FailedStage { get; init; }
    public int PendingVerifiedCount { get; init; }
    public int UnresolvedCount { get; init; }
    public DateOnly? TargetDate { get; init; }
    public Version? EdgeVersion { get; init; }
    public Version? PlaywrightVersion { get; init; }
}

/// <summary>Correlates asynchronous work for one run without persisting identities or secrets.</summary>
public sealed class PamExecutionAttempt
{
    private static readonly AsyncLocal<PamExecutionAttempt?> ambient = new();
    private readonly object gate = new();
    private DiagnosticAttemptSummary summary;
    private readonly ILogger? logger;

    /// <summary>Creates an independent attempt before preparation starts.</summary>
    public PamExecutionAttempt(int expectedCount = 0, ILogger? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);
        this.logger = logger;
        summary = new(Guid.NewGuid(), DateTimeOffset.UtcNow, null, "Running", "Preparation",
            expectedCount, 0, 0, 0, 0, 0, expectedCount, 0);
    }

    /// <summary>Gets the attempt of the current asynchronous workflow.</summary>
    public static PamExecutionAttempt? Current => ambient.Value;

    /// <summary>Activates this attempt only for the current asynchronous scope.</summary>
    public IDisposable Activate()
    {
        PamExecutionAttempt? previous = ambient.Value;
        ambient.Value = this;
        return new Scope(previous);
    }

    /// <summary>Returns a safe, immutable snapshot.</summary>
    public DiagnosticAttemptSummary Capture() { lock (gate) return summary; }

    /// <summary>Records the current phase and ordinal before interacting with a form.</summary>
    public void RecordStage(string stage, int? ordinal = null, int? expectedCount = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal ?? 0);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCount ?? 0);
        if (stage.Length is < 1 or > 64 || stage.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Use a safe stage identifier.", nameof(stage));
        lock (gate) summary = summary with
        {
            Stage = stage,
            FormOrdinal = ordinal ?? summary.FormOrdinal,
            ExpectedCount = expectedCount ?? summary.ExpectedCount,
            NotAttemptedCount = expectedCount.HasValue ? Math.Max(0, expectedCount.Value - summary.AcceptedCount - summary.ConflictCount - summary.RejectedCount - summary.UncertainCount - summary.FailedBeforeSubmissionCount) : summary.NotAttemptedCount
        };
        LogSnapshot();
    }

    private void LogSnapshot()
    {
        DiagnosticAttemptSummary state = Capture();
        // A diagnostic destination cannot invalidate an irreversible outcome.
        try
        {
            logger?.LogInformation("PAM attempt. Stage={Stage}, Status={Status}, FormOrdinal={FormOrdinal}, TargetDate={TargetDate}, ExpectedCount={ExpectedCount}, AcceptedCount={AcceptedCount}, ConflictCount={ConflictCount}, RejectedCount={RejectedCount}, UncertainCount={UncertainCount}, NotAttemptedCount={NotAttemptedCount}, FailedBeforeSubmissionCount={FailedBeforeSubmissionCount}, RequestIdCount={RequestIdCount}, PendingVerifiedCount={PendingVerifiedCount}, UnresolvedCount={UnresolvedCount}.",
                state.Stage, state.Status, state.FormOrdinal, state.TargetDate, state.ExpectedCount, state.AcceptedCount,
                state.ConflictCount, state.RejectedCount, state.UncertainCount, state.NotAttemptedCount,
                state.FailedBeforeSubmissionCount, state.RequestIdCount, state.PendingVerifiedCount, state.UnresolvedCount);
        }
        catch { }
    }

    public void RecordTargetDate(DateOnly target)
    {
        lock (gate) summary = summary with { TargetDate = target };
    }

    public void RecordBrowserVersions(Version? edge, Version? playwright)
    {
        lock (gate) summary = summary with { EdgeVersion = edge, PlaywrightVersion = playwright };
    }

    /// <summary>Records counts directly in the workflow, independently of UI progress.</summary>
    public void RecordCounts(int accepted, int conflicts, int rejected = 0, int uncertain = 0,
        int? notAttempted = null, int? requestIds = null, int failedBeforeSubmission = 0,
        int pendingVerified = 0, int? unresolved = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accepted);
        ArgumentOutOfRangeException.ThrowIfNegative(conflicts);
        ArgumentOutOfRangeException.ThrowIfNegative(rejected);
        ArgumentOutOfRangeException.ThrowIfNegative(uncertain);
        ArgumentOutOfRangeException.ThrowIfNegative(notAttempted ?? 0);
        ArgumentOutOfRangeException.ThrowIfNegative(requestIds ?? 0);
        ArgumentOutOfRangeException.ThrowIfNegative(failedBeforeSubmission);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingVerified);
        ArgumentOutOfRangeException.ThrowIfNegative(unresolved ?? 0);
        lock (gate) summary = summary with
        {
            AcceptedCount = accepted,
            ConflictCount = conflicts,
            RejectedCount = rejected,
            UncertainCount = uncertain,
            FailedBeforeSubmissionCount = failedBeforeSubmission,
            PendingVerifiedCount = pendingVerified,
            UnresolvedCount = unresolved ?? uncertain,
            NotAttemptedCount = notAttempted ?? Math.Max(0, summary.ExpectedCount - accepted - conflicts - rejected - uncertain - failedBeforeSubmission),
            RequestIdCount = requestIds ?? summary.RequestIdCount
        };
        LogSnapshot();
    }

    /// <summary>Preserves the original failing stage while read-only recovery advances.</summary>
    public void RecordFailureStage()
    {
        lock (gate) summary = summary with { FailedStage = summary.FailedStage ?? summary.Stage };
    }

    /// <summary>Closes the attempt with a safe terminal status, preserving the failed stage.</summary>
    public void Finish(string status)
    {
        if (status is not ("Succeeded" or "Failed" or "Canceled" or "Partial"))
            throw new ArgumentException("Unknown attempt status.", nameof(status));
        lock (gate) summary = summary with
        {
            Status = status,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            Stage = status == "Succeeded" ? summary.Stage : summary.FailedStage ?? summary.Stage
        };
        LogSnapshot();
    }

    private sealed class Scope(PamExecutionAttempt? previous) : IDisposable
    {
        public void Dispose() => ambient.Value = previous;
    }
}
