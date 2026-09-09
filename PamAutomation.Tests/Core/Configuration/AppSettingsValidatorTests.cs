using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests.Core.Configuration;

[TestClass]
[TestCategory("Unit")]
public sealed class AppSettingsValidatorTests
{
    private static readonly DateTimeOffset EcuadorNow =
        new(2026, 9, 9, 10, 0, 0, TimeSpan.FromHours(-5));

    private readonly AppSettingsValidator validator = new(new RequestSettingsValidator());

    [TestMethod]
    public void Defaults_NewSettings_AreSafe()
    {
        // Act
        var settings = new AppSettings();

        // Assert
        Assert.AreEqual(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.AreEqual(FinalAction.DisplayRequestIds, settings.FinalAction);
        Assert.AreEqual(AppSettings.EcuadorTimeZoneId, settings.TimeZoneId);
        Assert.IsFalse(settings.TestModeEnabled);
        Assert.IsNotNull(settings.Request);
    }

    [TestMethod]
    public void Constructor_NullRequestValidator_ThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => new AppSettingsValidator(null!));

        // Assert
        Assert.AreEqual("requestSettingsValidator", exception.ParamName);
    }

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
        Assert.Contains(
            ConfigurationValidationErrorCode.SettingsRequired,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    public void Validate_UnsupportedApplicationValues_ReturnsEveryFailure()
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            SchemaVersion = 2,
            FinalAction = (FinalAction)99,
            TimeZoneId = "UTC"
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.SchemaVersionUnsupported,
            result.Failures.Select(failure => failure.Code));
        Assert.Contains(
            ConfigurationValidationErrorCode.FinalActionUnsupported,
            result.Failures.Select(failure => failure.Code));
        Assert.Contains(
            ConfigurationValidationErrorCode.TimeZoneUnsupported,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    public void Validate_InvalidRequest_PropagatesRequestFailure()
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            Request = CreateValidSettings().Request with
            {
                RequestReason = string.Empty
            }
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.RequestReasonRequired,
            result.Failures.Select(failure => failure.Code));
    }

    [TestMethod]
    public void Validate_MissingRequest_ReturnsRequestRequiredFailure()
    {
        // Arrange
        var settings = CreateValidSettings() with
        {
            Request = null!
        };

        // Act
        var result = validator.Validate(settings, EcuadorNow);

        // Assert
        Assert.Contains(
            ConfigurationValidationErrorCode.RequestSettingsRequired,
            result.Failures.Select(failure => failure.Code));
    }

    private static AppSettings CreateValidSettings()
    {
        return new AppSettings
        {
            Request = new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 9),
                StartTime = new TimeOnly(10, 30),
                SessionHours = 4,
                RequestReason = "Soporte de producción"
            }
        };
    }
}
