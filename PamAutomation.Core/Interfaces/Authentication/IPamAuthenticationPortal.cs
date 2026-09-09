using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;

namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Defines the portal interactions required by the PAM authentication workflow.</summary>
public interface IPamAuthenticationPortal
{
    /// <summary>Submits PAM username and password to the current portal page.</summary>
    Task<PamCredentialSubmissionResult> SubmitCredentialsAsync(PamCredentials credentials, CancellationToken cancellationToken);
    /// <summary>Submits a generated TOTP code to the current portal page.</summary>
    Task<PamTotpSubmissionResult> SubmitTotpAsync(TotpCode code, CancellationToken cancellationToken);
    /// <summary>Waits until a visible TOTP rejection has disappeared.</summary>
    Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken);
    /// <summary>Waits for the final portal dashboard confirmation.</summary>
    Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken);
}
