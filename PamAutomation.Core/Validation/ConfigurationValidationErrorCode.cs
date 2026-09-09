namespace PamAutomation.Core.Validation;

/// <summary>
/// Identifies configuration validation failures without relying on display text.
/// </summary>
public enum ConfigurationValidationErrorCode
{
    /// <summary>The complete application settings value is missing.</summary>
    SettingsRequired,

    /// <summary>The request settings value is missing.</summary>
    RequestSettingsRequired,

    /// <summary>The schema version is not supported by the current domain model.</summary>
    SchemaVersionUnsupported,

    /// <summary>The final action is not a defined value.</summary>
    FinalActionUnsupported,

    /// <summary>The configured time zone is not the required Ecuador time zone.</summary>
    TimeZoneUnsupported,

    /// <summary>The configured start date is earlier than the current Ecuador date.</summary>
    StartDateInPast,

    /// <summary>The configured start time has already passed in Ecuador.</summary>
    StartTimeInPast,

    /// <summary>The session duration is outside the supported range.</summary>
    SessionHoursOutOfRange,

    /// <summary>The request reason is empty or contains only whitespace.</summary>
    RequestReasonRequired
}
