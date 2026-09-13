namespace PamAutomation.Infrastructure.Configuration;

/// <summary>
/// Identifies failures produced while loading or saving application settings.
/// </summary>
public enum AppSettingsPersistenceErrorCode
{
    /// <summary>The persisted JSON is malformed or incomplete.</summary>
    InvalidContent,

    /// <summary>The persisted schema is not supported by this application version.</summary>
    UnsupportedSchema,

    /// <summary>The configuration could not be read from local storage.</summary>
    ReadFailed,

    /// <summary>The configuration could not be written to local storage.</summary>
    WriteFailed
}
