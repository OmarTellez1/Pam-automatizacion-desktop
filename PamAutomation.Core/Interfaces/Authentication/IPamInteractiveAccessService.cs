using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Opens an independently owned browser and authenticates it for interactive PAM use.</summary>
public interface IPamInteractiveAccessService
{
    /// <summary>Opens BeyondInsight and leaves the authenticated browser available to the user.</summary>
    /// <param name="credentials">Protected PAM credentials loaded only for this operation.</param>
    /// <param name="cancellationToken">Cancels browser startup or authentication.</param>
    /// <returns>A task that completes after control of the authenticated browser is released.</returns>
    Task OpenAsync(PamCredentials credentials, CancellationToken cancellationToken = default);
}
