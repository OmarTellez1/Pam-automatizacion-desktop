using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Interfaces.Requests;

/// <summary>Exposes only the safe request-form operation used by test mode.</summary>
public interface IPamRequestTestPortal
{
    /// <summary>Opens, fills, verifies and discards one form without submitting it.</summary>
    Task ValidateAndDiscardAsync(
        PamFavoriteAccount account,
        RequestSettings settings,
        CancellationToken cancellationToken);
}
