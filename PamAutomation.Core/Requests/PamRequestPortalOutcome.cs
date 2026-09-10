namespace PamAutomation.Core.Requests;

/// <summary>Represents a classified, non-sensitive response from PAM.</summary>
public sealed record PamRequestPortalOutcome
{
    private PamRequestPortalOutcome(PamRequestSubmissionStatus status, string? conflictCode)
    {
        Status = status;
        ConflictCode = conflictCode;
    }

    /// <summary>Gets the recognized result.</summary>
    public PamRequestSubmissionStatus Status { get; }

    /// <summary>Gets the stable conflict code when no request was created.</summary>
    public string? ConflictCode { get; }

    /// <summary>Creates a successful submission outcome.</summary>
    public static PamRequestPortalOutcome Submitted() =>
        new(PamRequestSubmissionStatus.Submitted, conflictCode: null);

    /// <summary>Creates the recognized approved-access overlap outcome.</summary>
    public static PamRequestPortalOutcome ApprovedAccessOverlap() =>
        new(PamRequestSubmissionStatus.ApprovedAccessOverlap, PamRequestConflictCodes.ApprovedAccessOverlap);
}
