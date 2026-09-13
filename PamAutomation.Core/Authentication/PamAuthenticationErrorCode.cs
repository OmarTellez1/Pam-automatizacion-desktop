namespace PamAutomation.Core.Authentication;

/// <summary>Identifies a controlled PAM authentication failure without exposing credentials or TOTP values.</summary>
public enum PamAuthenticationErrorCode
{
    /// <summary>The portal explicitly rejected the PAM username or password.</summary>
    CredentialsRejected,
    /// <summary>The portal explicitly rejected both permitted TOTP attempts.</summary>
    TotpRejected,
    /// <summary>Connection or certificate communication with PAM failed.</summary>
    ConnectionOrCertificateFailure,
    /// <summary>The portal did not confirm authentication by showing the dashboard.</summary>
    ConfirmationMissing,
    /// <summary>The portal returned an unsupported result that cannot be safely retried.</summary>
    UnexpectedPortalResponse,
}
