namespace PamAutomation.Core.Requests;

/// <summary>Stable failure categories for reading and comparing <c>Pending</c>.</summary>
public enum PamPendingErrorCode
{
    PortalUnavailable,
    InvalidRequest,
    DuplicateRequestId,
    AmbiguousIdentity,
    FieldMismatch,
    /// <summary>The grid changed or did not stabilize within its observation window.</summary>
    SnapshotUnstable,
    /// <summary>The shared recovery deadline elapsed; requests must not be resubmitted.</summary>
    RecoveryTimeout,
}
