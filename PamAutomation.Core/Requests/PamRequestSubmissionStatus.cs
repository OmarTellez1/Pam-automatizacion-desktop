namespace PamAutomation.Core.Requests;

/// <summary>Identifies the recognized outcome of one PAM submission attempt.</summary>
public enum PamRequestSubmissionStatus
{
    /// <summary>PAM accepted the request.</summary>
    Submitted,
    /// <summary>PAM reports that approved access already overlaps the requested interval.</summary>
    ApprovedAccessOverlap,
    /// <summary>The row was never entered.</summary>
    NotAttempted,
    /// <summary>Validation failed before the irreversible click.</summary>
    FailedBeforeSubmission,
    /// <summary>PAM explicitly rejected the request.</summary>
    Rejected,
    /// <summary>The click may have happened but its outcome is unknown.</summary>
    Uncertain,
}
