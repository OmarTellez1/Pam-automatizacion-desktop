using System.Globalization;
using PamAutomation.Core.Interfaces.Time;

namespace PamAutomation.Infrastructure.Time;

/// <summary>
/// Creates trusted clocks calibrated from the PAM portal HTTP <c>Date</c> header.
/// </summary>
public sealed class PamHttpDateClockFactory : IPamClockFactory
{
    private static readonly TimeSpan HttpDateResolutionAllowance = TimeSpan.FromMilliseconds(500);
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PamHttpDateClockFactory"/> class.
    /// </summary>
    /// <param name="timeProvider">The monotonic time source used after calibration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public PamHttpDateClockFactory(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Creates a clock calibrated to the estimated instant at which the PAM portal response was received.
    /// </summary>
    /// <param name="httpDateHeader">The PAM portal HTTP <c>Date</c> header in RFC 1123 format.</param>
    /// <param name="roundTripTime">The elapsed time from sending the request until receiving its response.</param>
    /// <returns>A clock that advances from the calibrated trusted time.</returns>
    /// <exception cref="PamClockException">The HTTP date is unavailable or invalid, or the round-trip measurement is invalid.</exception>
    public IClock Create(string? httpDateHeader, TimeSpan roundTripTime)
    {
        if (string.IsNullOrWhiteSpace(httpDateHeader) ||
            !DateTimeOffset.TryParseExact(
                httpDateHeader,
                "R",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset portalDate))
        {
            throw new PamClockException(
                PamClockErrorCode.HttpDateUnavailableOrInvalid,
                "El portal PAM no proporcionó una hora HTTP válida.");
        }

        if (roundTripTime < TimeSpan.Zero)
        {
            throw new PamClockException(
                PamClockErrorCode.InvalidRoundTripTime,
                "La medición de tiempo de ida y vuelta del portal PAM no es válida.");
        }

        DateTimeOffset receivedAt = portalDate
            .ToUniversalTime()
            .Add(HttpDateResolutionAllowance)
            .Add(roundTripTime / 2);

        return new MonotonicPamClock(receivedAt, timeProvider, timeProvider.GetTimestamp());
    }

    private sealed class MonotonicPamClock : IClock
    {
        private readonly DateTimeOffset calibratedUtcNow;
        private readonly TimeProvider timeProvider;
        private readonly long calibrationTimestamp;

        public MonotonicPamClock(
            DateTimeOffset calibratedUtcNow,
            TimeProvider timeProvider,
            long calibrationTimestamp)
        {
            this.calibratedUtcNow = calibratedUtcNow;
            this.timeProvider = timeProvider;
            this.calibrationTimestamp = calibrationTimestamp;
        }

        /// <inheritdoc/>
        public DateTimeOffset UtcNow => calibratedUtcNow.Add(
            timeProvider.GetElapsedTime(calibrationTimestamp, timeProvider.GetTimestamp()));
    }
}
