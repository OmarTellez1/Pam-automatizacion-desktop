namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Represents the portal's explicit response to TOTP submission.</summary>
public enum PamTotpSubmissionResult
{
    /// <summary>The portal accepted the TOTP submission.</summary>
    Accepted,
    /// <summary>The portal explicitly rejected the TOTP submission.</summary>
    Rejected,
}
