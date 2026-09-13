using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Creates temporary PAM browser sessions whose login has been confirmed.</summary>
public interface IPamAuthenticatedSessionFactory
{
    /// <summary>Creates and authenticates a session that the caller must dispose.</summary>
    /// <param name="credentials">Protected PAM credentials loaded for this operation.</param>
    /// <param name="cancellationToken">Cancels browser launch or authentication.</param>
    /// <returns>An authenticated temporary browser session owned by the caller.</returns>
    Task<IPamBrowserSession> CreateAuthenticatedAsync(
        PamCredentials credentials,
        CancellationToken cancellationToken = default);
}
