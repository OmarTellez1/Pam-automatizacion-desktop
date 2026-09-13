using PamAutomation.Core.Requests;

namespace PamAutomation.Core.Interfaces.Requests;

/// <summary>Reads PAM requests currently shown under the <c>Pending</c> status.</summary>
public interface IPamPendingPortal
{
    /// <summary>Reads every page of the current user's <c>Pending</c> requests.</summary>
    Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(CancellationToken cancellationToken);
}
