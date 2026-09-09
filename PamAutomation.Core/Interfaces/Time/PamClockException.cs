namespace PamAutomation.Core.Interfaces.Time;

/// <summary>
/// Represents a safe, controlled failure while obtaining a trusted PAM time.
/// </summary>
public sealed class PamClockException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PamClockException"/> class.
    /// </summary>
    /// <param name="errorCode">Stable category for the clock calibration failure.</param>
    /// <param name="message">Description that does not contain HTTP header values.</param>
    /// <param name="innerException">Original technical exception, when available.</param>
    public PamClockException(
        PamClockErrorCode errorCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the stable category for the clock calibration failure.
    /// </summary>
    public PamClockErrorCode ErrorCode { get; }
}
