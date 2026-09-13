namespace PamAutomation.Infrastructure.Configuration;

/// <summary>
/// Represents a controlled failure while loading or saving application settings.
/// </summary>
public sealed class AppSettingsPersistenceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsPersistenceException"/> class.
    /// </summary>
    /// <param name="errorCode">Stable category for the persistence failure.</param>
    /// <param name="message">Safe description of the failure.</param>
    /// <param name="innerException">Original exception, when available.</param>
    public AppSettingsPersistenceException(
        AppSettingsPersistenceErrorCode errorCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the stable category for the persistence failure.
    /// </summary>
    public AppSettingsPersistenceErrorCode ErrorCode { get; }
}
