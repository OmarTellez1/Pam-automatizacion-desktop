namespace PamAutomation.Core.Requests;

/// <summary>Contains recognized, non-fatal PAM request conflict codes.</summary>
public static class PamRequestConflictCodes
{
    /// <summary>PAM reports that approved access already covers the requested interval.</summary>
    public const string ApprovedAccessOverlap =
        "PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable";
}
