using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Totp;

namespace PamAutomation.Tests.Core.Totp;

[TestClass]
[TestCategory("Unit")]
public sealed class TotpGeneratorTests
{
    private const string RfcTestSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private readonly TotpGenerator generator = new();

    [TestMethod]
    [DataRow(59L, "287082")]
    [DataRow(1_111_111_109L, "081804")]
    [DataRow(1_111_111_111L, "050471")]
    [DataRow(1_234_567_890L, "005924")]
    [DataRow(2_000_000_000L, "279037")]
    [DataRow(20_000_000_000L, "353130")]
    public void Generate_Rfc6238Sha1Vector_ReturnsExpectedSixDigits(
        long unixSeconds,
        string expectedCode)
    {
        // The application uses the six least-significant digits of the RFC 6238 SHA-1 vectors.
        // Arrange
        DateTimeOffset referenceTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        // Act
        TotpCode result = generator.Generate(RfcTestSecret, referenceTime);

        // Assert
        Assert.AreEqual(expectedCode, result.Value);
        Assert.HasCount(6, result.Value);
        Assert.IsTrue(result.Value.All(char.IsAsciiDigit));
    }

    [TestMethod]
    public void Generate_GroupedLowercaseSecret_AcceptsAuthenticatorDisplayFormat()
    {
        // Arrange
        const string groupedSecret = "gezd gnbv gy3t qojq gezd gnbv gy3t qojq";
        DateTimeOffset referenceTime = DateTimeOffset.FromUnixTimeSeconds(59);

        // Act
        TotpCode result = generator.Generate(groupedSecret, referenceTime);

        // Assert
        Assert.AreEqual("287082", result.Value);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("A")]
    [DataRow("ABC0")]
    [DataRow("M=ZXW6===")]
    [DataRow("MZ======")]
    public void Generate_InvalidBase32Secret_ThrowsSafeFormatException(string invalidSecret)
    {
        // Act
        InvalidBase32SecretException exception =
            Assert.ThrowsExactly<InvalidBase32SecretException>(
                () => generator.Generate(invalidSecret, DateTimeOffset.UnixEpoch));

        // Assert
        Assert.AreEqual(
            "La clave de enrolamiento TOTP no tiene un formato Base32 válido.",
            exception.Message);
    }

    [TestMethod]
    public void Generate_NullSecret_ThrowsArgumentNullException()
    {
        // Act
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => generator.Generate(null!, DateTimeOffset.UnixEpoch));

        // Assert
        Assert.AreEqual("base32Secret", exception.ParamName);
    }

    [TestMethod]
    public void Generate_TimesInSameWindow_ReturnsSameCodeAndWindow()
    {
        // Act
        TotpCode first = generator.Generate(
            RfcTestSecret,
            DateTimeOffset.FromUnixTimeSeconds(30));
        TotpCode last = generator.Generate(
            RfcTestSecret,
            DateTimeOffset.FromUnixTimeSeconds(59));

        // Assert
        Assert.AreEqual(first.Value, last.Value);
        Assert.AreEqual(first.ValidFrom, last.ValidFrom);
        Assert.AreEqual(first.ExpiresAt, last.ExpiresAt);
    }

    [TestMethod]
    public void Generate_TimeInNextWindow_ReturnsNewWindow()
    {
        // Act
        TotpCode first = generator.Generate(
            RfcTestSecret,
            DateTimeOffset.FromUnixTimeSeconds(59));
        TotpCode next = generator.Generate(
            RfcTestSecret,
            DateTimeOffset.FromUnixTimeSeconds(60));

        // Assert
        Assert.AreNotEqual(first.Value, next.Value);
        Assert.AreEqual(first.ExpiresAt, next.ValidFrom);
    }

    [TestMethod]
    public void Generate_TimeBeforeUnixEpoch_ThrowsArgumentOutOfRangeException()
    {
        // Act
        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => generator.Generate(
                    RfcTestSecret,
                    DateTimeOffset.UnixEpoch.AddSeconds(-1)));

        // Assert
        Assert.AreEqual("referenceTime", exception.ParamName);
    }

    [TestMethod]
    [DataRow(21L, true)]
    [DataRow(22L, true)]
    [DataRow(23L, false)]
    public void HasMinimumRemainingLifetime_EightSecondPolicy_ReturnsExpectedResult(
        long secondsIntoWindow,
        bool expected)
    {
        // Arrange
        DateTimeOffset windowStart = DateTimeOffset.FromUnixTimeSeconds(1_000_000_020);
        TotpCode code = generator.Generate(RfcTestSecret, windowStart);

        // Act
        bool result = code.HasMinimumRemainingLifetime(
            windowStart.AddSeconds(secondsIntoWindow),
            TotpGenerator.MinimumSubmissionLifetime);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(31)]
    public void HasMinimumRemainingLifetime_InvalidMinimum_ThrowsArgumentOutOfRangeException(
        int minimumSeconds)
    {
        // Arrange
        TotpCode code = generator.Generate(RfcTestSecret, DateTimeOffset.UnixEpoch);

        // Act
        ArgumentOutOfRangeException exception =
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => code.HasMinimumRemainingLifetime(
                    DateTimeOffset.UnixEpoch,
                    TimeSpan.FromSeconds(minimumSeconds)));

        // Assert
        Assert.AreEqual("minimumLifetime", exception.ParamName);
    }

    [TestMethod]
    public void ToString_GeneratedCode_DoesNotExposeValue()
    {
        // Arrange
        TotpCode code = generator.Generate(
            RfcTestSecret,
            DateTimeOffset.FromUnixTimeSeconds(59));

        // Act
        string description = code.ToString();

        // Assert
        Assert.IsFalse(description.Contains(code.Value, StringComparison.Ordinal));
    }
}
