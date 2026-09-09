namespace PamAutomation.Core.Interfaces.Time;

/// <summary>
/// Identifies controlled failures while calibrating the trusted PAM clock.
/// </summary>
public enum PamClockErrorCode
{
    /// <summary>The PAM portal did not provide an HTTP date, or the provided value was not a valid RFC 1123 date.</summary>
    HttpDateUnavailableOrInvalid,

    /// <summary>The measured request round-trip time was not valid for calibration.</summary>
    InvalidRoundTripTime
}
