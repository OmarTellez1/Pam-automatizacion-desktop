namespace PamAutomation.Core.Requests;

/// <summary>Identifies controlled failures in PAM request test mode.</summary>
public enum PamRequestTestErrorCode
{
    /// <summary>The request configuration is not valid.</summary>
    InvalidSettings,
    /// <summary>The portal could not safely validate and discard every form.</summary>
    PortalUnavailable,
}
