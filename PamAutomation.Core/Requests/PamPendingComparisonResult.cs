using System.Diagnostics;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Requests;

/// <summary>Contains in-memory matches and missing accounts for one requested start time.</summary>
[DebuggerDisplay("PAM Pending comparison: {MatchedCount} matched, {MissingCount} missing")]
public sealed record PamPendingComparisonResult
{
    /// <summary>Initializes a defensive snapshot of the comparison.</summary>
    public PamPendingComparisonResult(
        IReadOnlyList<PamPendingRequestMatch> matches,
        IReadOnlyList<PamFavoriteAccount> missingAccounts)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(missingAccounts);
        if (matches.Any(static match => match is null) ||
            missingAccounts.Any(static account => account is null))
        {
            throw new ArgumentException("La comparación Pending no admite elementos nulos.");
        }

        Matches = matches.ToArray();
        MissingAccounts = missingAccounts.ToArray();
    }

    /// <summary>Gets the uniquely matched requests.</summary>
    public IReadOnlyList<PamPendingRequestMatch> Matches { get; }

    /// <summary>Gets expected accounts without a matching request.</summary>
    public IReadOnlyList<PamFavoriteAccount> MissingAccounts { get; }

    /// <summary>Gets matched Request ID in the same order as the expected accounts.</summary>
    public IReadOnlyList<string> RequestIds => Matches.Select(static match => match.Request.RequestId).ToArray();

    public int MatchedCount => Matches.Count;

    public int MissingCount => MissingAccounts.Count;

    public bool IsComplete => MissingCount == 0;

    /// <inheritdoc />
    public override string ToString() =>
        $"PAM Pending comparison: {MatchedCount} matched, {MissingCount} missing";
}
