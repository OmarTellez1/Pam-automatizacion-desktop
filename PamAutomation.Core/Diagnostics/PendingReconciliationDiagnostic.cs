using PamAutomation.Core.Favorites;
using PamAutomation.Core.Requests;

namespace PamAutomation.Core.Diagnostics;

/// <summary>Contains the explicitly authorized detailed trace of one Pending reconciliation.</summary>
public sealed record PendingReconciliationDiagnostic(
    int SchemaVersion,
    string ExecutionId,
    DateTimeOffset StartedAtUtc,
    string Decision,
    int ExactPendingDuplicateCount,
    IReadOnlyList<PendingExpectedRequestDiagnostic> ExpectedRequests,
    PendingSnapshotDiagnostic BeforeSubmission,
    int SubmittedCount,
    IReadOnlyList<PendingComparisonAttemptDiagnostic> Attempts,
    string? FailureCode)
{
    /// <summary>Prevents authorized diagnostic fields from leaking through incidental structured logging.</summary>
    public override string ToString() => "Pending reconciliation diagnostic (explicit export only)";
}

/// <summary>Contains one expected request without authentication or Outlook secrets.</summary>
public sealed record PendingExpectedRequestDiagnostic(
    string System,
    string Application,
    string Account,
    DateOnly StartDate,
    TimeOnly StartTime,
    DateOnly ExpiresDate,
    TimeOnly ExpiresTime,
    string Description)
{
    /// <summary>Prevents authorized diagnostic fields from leaking through incidental structured logging.</summary>
    public override string ToString() => "Expected Pending request diagnostic (explicit export only)";

    /// <summary>Creates an expected diagnostic row from validated request data.</summary>
    public static PendingExpectedRequestDiagnostic Create(
        PamFavoriteAccount account,
        PamRequestFormData form)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(form);
        DateTime expiration = form.StartDate.ToDateTime(form.StartTime).AddMinutes(form.TotalMinutes);
        return new(
            account.SystemName,
            account.ApplicationName,
            account.AccountName,
            form.StartDate,
            form.StartTime,
            DateOnly.FromDateTime(expiration),
            TimeOnly.FromDateTime(expiration),
            form.Reason);
    }
}

/// <summary>Contains one real Pending row included only in an explicitly exported diagnostic.</summary>
public sealed record PendingRequestDiagnostic(
    string RequestId,
    string System,
    string Application,
    string Account,
    DateOnly StartDate,
    TimeOnly StartTime,
    DateOnly ExpiresDate,
    TimeOnly ExpiresTime,
    string Description)
{
    /// <summary>Prevents authorized diagnostic fields from leaking through incidental structured logging.</summary>
    public override string ToString() => "Pending request diagnostic (explicit export only)";

    /// <summary>Creates a diagnostic row from a validated Pending row.</summary>
    public static PendingRequestDiagnostic Create(PamPendingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(
            request.RequestId,
            request.SystemName,
            request.ApplicationName,
            request.AccountName,
            request.StartDate,
            request.StartTime,
            request.ExpiresDate,
            request.ExpiresTime,
            request.Description);
    }
}

/// <summary>Contains one complete Pending snapshot and its ID statistics.</summary>
public sealed record PendingSnapshotDiagnostic(
    DateTimeOffset CapturedAtUtc,
    int RowCount,
    int UniqueRequestIdCount,
    IReadOnlyList<PendingRequestDiagnostic> Requests)
{
    /// <summary>Prevents authorized diagnostic fields from leaking through incidental structured logging.</summary>
    public override string ToString() => "Pending snapshot diagnostic (explicit export only)";
}

/// <summary>Contains one post-submission comparison attempt and the computed set difference.</summary>
public sealed record PendingComparisonAttemptDiagnostic(
    int Attempt,
    DateTimeOffset CapturedAtUtc,
    int RowCount,
    int UniqueRequestIdCount,
    int BaselineIntersectionCount,
    int MissingBaselineCount,
    int NewRequestCount,
    int ExactBatchCandidateCount,
    int UnexpectedNewRequestCount,
    int MatchedCount,
    int MissingExpectedCount,
    string? ErrorCode,
    IReadOnlyList<PendingGroupMultiplicityDiagnostic> Groups,
    IReadOnlyList<PendingRequestDiagnostic> Requests,
    IReadOnlyList<PendingRequestDiagnostic> NewRequests)
{
    /// <summary>Prevents authorized diagnostic fields from leaking through incidental structured logging.</summary>
    public override string ToString() => "Pending comparison attempt diagnostic (explicit export only)";
}

/// <summary>Explains expected and observed multiplicity for one stable Pending identity.</summary>
public sealed record PendingGroupMultiplicityDiagnostic(
    string System,
    string Account,
    int ExpectedCount,
    int CandidateCount)
{
    /// <summary>Prevents authorized diagnostic fields from leaking through incidental structured logging.</summary>
    public override string ToString() => "Pending multiplicity diagnostic (explicit export only)";
}
