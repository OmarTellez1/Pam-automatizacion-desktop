namespace PamAutomation.Core.Models.Configuration;

/// <summary>
/// Contains the complete non-sensitive application configuration.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// Identifies the schema understood by the current application version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Identifies the required IANA time zone for request configuration.
    /// </summary>
    public const string EcuadorTimeZoneId = "America/Guayaquil";

    /// <summary>
    /// Gets the persisted configuration schema version.
    /// </summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets the action performed after a successful PAM execution.
    /// </summary>
    public FinalAction FinalAction { get; init; } = FinalAction.DisplayRequestIds;

    /// <summary>
    /// Gets the PAM request configuration.
    /// </summary>
    public RequestSettings Request { get; init; } = new();

    /// <summary>
    /// Gets the IANA time zone used to interpret request dates and times.
    /// </summary>
    public string TimeZoneId { get; init; } = EcuadorTimeZoneId;

    /// <summary>
    /// Gets a value indicating whether the safe PAM test action may be shown.
    /// </summary>
    public bool TestModeEnabled { get; init; }
}
