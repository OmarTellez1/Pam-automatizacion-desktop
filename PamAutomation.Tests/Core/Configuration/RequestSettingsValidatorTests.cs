using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests.Core.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class RequestSettingsValidatorTests
{
    private static readonly DateTimeOffset EcuadorNow =
        new(2026, 9, 9, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly RequestSettingsValidator validator = new();

    [TestMethod]
    public void Validate_ValidSettings_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Failures);
    }

    [TestMethod]
    public void Validate_NullSettings_ReturnsRequiredFailure()
    {
        // Act
        var result = validator.Validate(null, EcuadorNow);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            ConfigurationValidationErrorCode.RequestSettingsRequired,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    public void Validate_DateBeforeEcuadorToday_ReturnsDateFailure()
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            StartDate = new DateOnly(2026, 9, 8)
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.StartDateInPast,
            result.Failures.Select(failure => failure.Code));
        Assert.DoesNotContain(
            ConfigurationValidationErrorCode.StartTimeInPast,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    public void Validate_TimeBeforeEcuadorNowOnSameDate_ReturnsTimeFailure()
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            StartTime = new TimeOnly(9, 59)
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.StartTimeInPast,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(9)]
    public void Validate_DurationOutsideOneToEightHours_ReturnsDurationFailure(int sessionHours)
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            SessionHours = sessionHours
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.SessionHoursOutOfRange,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    public void Validate_DurationAtSupportedBoundary_ReturnsSuccess(int sessionHours)
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            SessionHours = sessionHours
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_WhitespaceReason_ReturnsReasonFailure()
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            RequestReason = "   "
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.RequestReasonRequired,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    public void Validate_MultipleInvalidValues_ReturnsEveryFailure()
    {
        // Arrange
        var settings = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 8),
            StartTime = new TimeOnly(9, 0),
            SessionHours = 10,
            RequestReason = string.Empty
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.HasCount(3, result.Failures);
    }

    private static RequestSettings CreateValidSettings()
    {
        return new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 9),
            StartTime = new TimeOnly(10, 30),
            SessionHours = 4,
            RequestReason = "Soporte de producción"
        };
    }
}
