namespace PamAutomation.Core.Interfaces.Time;

/// <summary>
/// Creates clocks calibrated from the trusted HTTP <c>Date</c> response header of the PAM portal.
/// </summary>
public interface IPamClockFactory
{
    /// <summary>
    /// Creates a clock calibrated to the estimated instant at which a PAM portal response was received.
    /// </summary>
    /// <param name="httpDateHeader">The PAM portal HTTP <c>Date</c> header in RFC 1123 format.</param>
    /// <param name="roundTripTime">The elapsed time from sending the request until receiving its response.</param>
    /// <returns>A clock that advances from the calibrated trusted time.</returns>
    /// <exception cref="PamClockException">The HTTP date is unavailable or invalid, or the round-trip measurement is invalid.</exception>
    IClock Create(string? httpDateHeader, TimeSpan roundTripTime);
}
