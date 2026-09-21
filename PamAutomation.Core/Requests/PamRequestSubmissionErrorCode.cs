namespace PamAutomation.Core.Requests;

/// <summary>Identifies controlled failures during real PAM request submission.</summary>
public enum PamRequestSubmissionErrorCode
{
    /// <summary>The persisted operational settings are invalid.</summary>
    InvalidSettings,
    /// <summary>The observed <c>Favorites</c> count differs from the confirmed count.</summary>
    AuthorizationMismatch,
    /// <summary>PAM rejected a request with an unrecognized response.</summary>
    RequestRejected,
    /// <summary>The portal could not safely complete the workflow.</summary>
    PortalUnavailable,
    /// <summary>PAM accepted requests, but their new Request IDs could not be reconciled safely.</summary>
    ReconciliationFailedAfterSubmission,
}
