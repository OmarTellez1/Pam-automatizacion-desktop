using System.Diagnostics;

namespace PamAutomation.Core.Requests;

/// <summary>Contains the in-memory results of one confirmed real PAM run.</summary>
[DebuggerDisplay("PAM request run: {SubmittedCount} submitted, {ConflictCount} conflicts")]
public sealed record PamRequestRunResult
{
    /// <summary>Initializes a run result from every processed <c>Favorites</c> row.</summary>
    /// <param name="accounts">Per-row outcomes kept only in memory.</param>
    public PamRequestRunResult(IReadOnlyList<PamRequestAccountResult> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        if (accounts.Any(static account => account is null))
        {
            throw new ArgumentException("The result list cannot contain null entries.", nameof(accounts));
        }

        Accounts = Array.AsReadOnly(accounts.ToArray());
    }

    /// <summary>Gets every per-row outcome.</summary>
    public IReadOnlyList<PamRequestAccountResult> Accounts { get; }

    /// <summary>Gets the number of accepted submissions.</summary>
    public int SubmittedCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.Submitted);

    /// <summary>Gets the number of recognized approved-access overlaps.</summary>
    public int ConflictCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.ApprovedAccessOverlap);

    public int UncertainCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.Uncertain);
    public int UnresolvedCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.Uncertain && !result.ConfirmedInPending);
    public int PendingVerifiedCount => Accounts.Count(result => result.ConfirmedInPending);
    public int RejectedCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.Rejected);
    public int NotAttemptedCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.NotAttempted);
    public int FailedBeforeSubmissionCount => Accounts.Count(result => result.Status == PamRequestSubmissionStatus.FailedBeforeSubmission);
    public bool IsComplete => SubmittedCount + ConflictCount == Accounts.Count;

    /// <inheritdoc />
    public override string ToString() =>
        $"PAM request run: {SubmittedCount} submitted, {ConflictCount} conflicts";
}
