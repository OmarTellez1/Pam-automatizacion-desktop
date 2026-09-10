using System.Diagnostics;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Requests;

/// <summary>Associates one in-memory <c>Favorites</c> identity with its submission outcome.</summary>
[DebuggerDisplay("PAM request account result (redacted): {Status}")]
public sealed record PamRequestAccountResult(
    PamFavoriteAccount Account,
    PamRequestSubmissionStatus Status,
    string? ConflictCode)
{
    /// <inheritdoc />
    public override string ToString() => $"PAM request account result (redacted): {Status}";
}
