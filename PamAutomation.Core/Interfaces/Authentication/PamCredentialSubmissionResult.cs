namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Represents the portal's explicit response to username and password submission.</summary>
public enum PamCredentialSubmissionResult
{
    /// <summary>The portal accepted the credentials and requested TOTP.</summary>
    Accepted,
    /// <summary>The portal explicitly rejected the credentials.</summary>
    Rejected,
}
