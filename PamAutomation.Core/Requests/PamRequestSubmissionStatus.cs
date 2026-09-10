namespace PamAutomation.Core.Requests;

/// <summary>Identifies the recognized outcome of one PAM submission attempt.</summary>
public enum PamRequestSubmissionStatus
{
    /// <summary>PAM accepted the request.</summary>
    Submitted,
    /// <summary>PAM reports that approved access already overlaps the requested interval.</summary>
    ApprovedAccessOverlap,
}
