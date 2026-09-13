using PamAutomation.Core.Favorites;
using PamAutomation.Core.Requests;

namespace PamAutomation.Core.Interfaces.Requests;

/// <summary>Submits explicitly authorized PAM request forms.</summary>
public interface IPamRequestSubmissionPortal
{
    /// <summary>Validates and submits one request form.</summary>
    /// <param name="account">Exact <c>Favorites</c> row identity to reopen and validate.</param>
    /// <param name="form">Values to validate immediately before submission.</param>
    /// <param name="cancellationToken">Cancels navigation before an irreversible action when possible.</param>
    /// <returns>The classified portal outcome.</returns>
    Task<PamRequestPortalOutcome> SubmitAsync(
        PamFavoriteAccount account,
        PamRequestFormData form,
        CancellationToken cancellationToken);
}
