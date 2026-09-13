using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Tests.Core.Credentials;

[TestClass]
[TestCategory("Unit")]
public sealed class PamCredentialsTests
{
    [TestMethod]
    public void Constructor_CompleteValues_PreservesValues()
    {
        // Act
        var credentials = new PamCredentials(
            "fake-user",
            "fake-password",
            "JBSWY3DPEHPK3PXP");

        // Assert
        Assert.AreEqual("fake-user", credentials.Username);
        Assert.AreEqual("fake-password", credentials.Password);
        Assert.AreEqual("JBSWY3DPEHPK3PXP", credentials.TotpSeed);
    }

    [TestMethod]
    public void Constructor_WhitespaceValue_ThrowsArgumentException()
    {
        // Act
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new PamCredentials("fake-user", " ", "JBSWY3DPEHPK3PXP"));

        // Assert
        Assert.AreEqual("password", exception.ParamName);
    }

    [TestMethod]
    public void ToString_CompleteCredentials_DoesNotExposeValues()
    {
        // Arrange
        var credentials = new PamCredentials(
            "fake-user",
            "fake-password",
            "JBSWY3DPEHPK3PXP");

        // Act
        string description = credentials.ToString();

        // Assert
        Assert.IsFalse(description.Contains("fake-user", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("fake-password", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("JBSWY3DPEHPK3PXP", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UpdateToString_CompleteUpdate_DoesNotExposeValues()
    {
        // Arrange
        var update = new PamCredentialUpdate
        {
            Username = "fake-user",
            Password = "fake-password",
            TotpSeed = "JBSWY3DPEHPK3PXP"
        };

        // Act
        string description = update.ToString();

        // Assert
        Assert.IsFalse(description.Contains("fake-user", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("fake-password", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("JBSWY3DPEHPK3PXP", StringComparison.Ordinal));
    }
}
