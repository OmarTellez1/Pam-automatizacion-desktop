namespace PamAutomation.Core.Requests;

/// <summary>Stable failure categories for reading and comparing <c>Pending</c>.</summary>
public enum PamPendingErrorCode
{
    PortalUnavailable,
    InvalidRequest,
    DuplicateRequestId,
    AmbiguousIdentity,
    FieldMismatch,
}
