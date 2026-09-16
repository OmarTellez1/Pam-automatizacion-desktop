using PamAutomation.Core.Accounts;

namespace PamAutomation.Tests.Core.Accounts;

[TestClass]
[TestCategory("Unit")]
public sealed class PamAccountDomainTests
{
    [TestMethod]
    public void Registry_WithOnePrimaryAndSecondary_IsValid()
    {
        var principal = new PamAccountSummary(PamAccountId.New(), " Principal ", true);
        var secondary = new PamAccountSummary(PamAccountId.New(), "Soporte", false);

        var state = new PamAccountRegistryState([principal, secondary]);

        Assert.AreEqual("Principal", state.PrimaryAccount?.Name);
        Assert.HasCount(2, state.Accounts);
    }

    [TestMethod]
    public void Registry_WithoutPrincipalOrWithTwoPrincipals_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PamAccountRegistryState(
            [new PamAccountSummary(PamAccountId.New(), "Secundario", false)]));
        Assert.ThrowsExactly<ArgumentException>(() => new PamAccountRegistryState(
            [
                new PamAccountSummary(PamAccountId.New(), "Uno", true),
                new PamAccountSummary(PamAccountId.New(), "Dos", true)
            ]));
    }

    [TestMethod]
    public void Registry_DuplicateNamesIgnoringCase_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PamAccountRegistryState(
            [
                new PamAccountSummary(PamAccountId.New(), "Omar", true),
                new PamAccountSummary(PamAccountId.New(), "OMAR", false)
            ]));
    }
}
