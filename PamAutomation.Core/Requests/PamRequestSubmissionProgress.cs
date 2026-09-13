using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Requests;

/// <summary>Describes one request submission outcome reported while a real execution is running.</summary>
/// <param name="Account">Favorite account associated with the completed attempt.</param>
/// <param name="Status">Recognized outcome returned by PAM.</param>
/// <param name="ProcessedCount">Number of Favorites rows processed so far.</param>
/// <param name="TotalCount">Total Favorites rows included in the authorized run.</param>
public sealed record PamRequestSubmissionProgress(
    PamFavoriteAccount Account,
    PamRequestSubmissionStatus Status,
    int ProcessedCount,
    int TotalCount);
