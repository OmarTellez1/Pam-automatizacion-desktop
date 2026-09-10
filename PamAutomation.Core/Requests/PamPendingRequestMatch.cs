using System.Diagnostics;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Requests;

/// <summary>Associates one expected <c>Favorites</c> row with one <c>Pending</c> request.</summary>
[DebuggerDisplay("PAM Pending match (redacted)")]
public sealed record PamPendingRequestMatch(PamFavoriteAccount Account, PamPendingRequest Request)
{
    /// <inheritdoc />
    public override string ToString() => "PAM Pending match (redacted)";
}
