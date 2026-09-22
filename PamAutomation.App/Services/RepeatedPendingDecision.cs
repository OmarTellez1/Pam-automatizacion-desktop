namespace PamAutomation.App.Services;

/// <summary>Identifies the user decision when exact requests already exist in Pending.</summary>
public enum RepeatedPendingDecision
{
    Cancel,
    UseExistingRequestIds,
}
