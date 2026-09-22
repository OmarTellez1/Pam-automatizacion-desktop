using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Resilience;

namespace PamAutomation.Tests.Core.Resilience;

[TestClass]
[TestCategory("Unit")]
public sealed class PamOperationTimingPolicyTests
{
    [TestMethod]
    public void PortalPolicy_PollsQuicklyButAllowsSlowCorporateResponses()
    {
        Assert.IsGreaterThan(
            PamOperationTimingPolicy.ObservationInterval,
            PamOperationTimingPolicy.PortalReadinessTimeout);
        Assert.IsGreaterThan(
            PamOperationTimingPolicy.TransientProbeTimeout,
            PamOperationTimingPolicy.PortalReadinessTimeout);
        Assert.IsLessThanOrEqualTo(
            TimeSpan.FromMilliseconds(250),
            PamOperationTimingPolicy.ObservationInterval);
    }

    [TestMethod]
    public void ExternalConsistencyPolicies_AreBoundedAndHaveMultipleObservations()
    {
        Assert.IsGreaterThanOrEqualTo(2, PamOperationTimingPolicy.PendingComparisonMaximumAttempts);
        Assert.IsGreaterThanOrEqualTo(2, PamOperationTimingPolicy.PendingReadMaximumAttemptsPerSession);
        Assert.IsGreaterThanOrEqualTo(2, PamOperationTimingPolicy.PendingReadSessionMaximumAttempts);
        Assert.IsGreaterThanOrEqualTo(2, PamOperationTimingPolicy.OutlookConfirmationMaximumAttempts);
        Assert.IsGreaterThanOrEqualTo(2, PamOperationTimingPolicy.OutlookStartupMaximumAttempts);
        Assert.IsGreaterThan(TimeSpan.Zero, PamOperationTimingPolicy.PendingComparisonRetryDelay);
        Assert.IsGreaterThan(TimeSpan.Zero, PamOperationTimingPolicy.PendingReadRecoveryDelay);
        Assert.IsGreaterThan(TimeSpan.Zero, PamOperationTimingPolicy.OutlookConfirmationPollInterval);
        Assert.IsGreaterThan(TimeSpan.Zero, PamOperationTimingPolicy.OutlookStartupRetryDelay);
    }
}
