using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Interfaces.Time;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Infrastructure.Time;

[TestClass]
[TestCategory("Unit")]
public sealed class PamHttpDateClockFactoryTests
{
    private const string PortalDateHeader = "Wed, 21 Oct 2015 07:28:00 GMT";

    [TestMethod]
    public void Create_ZeroRoundTrip_CalibratesWithHttpDateResolutionAllowance()
    {
        // Arrange
        var timeProvider = new ControlledTimeProvider();
        var factory = new PamHttpDateClockFactory(timeProvider);

        // Act
        IClock clock = factory.Create(PortalDateHeader, TimeSpan.Zero);

        // Assert
        Assert.AreEqual(
            new DateTimeOffset(2015, 10, 21, 7, 28, 0, TimeSpan.Zero).AddMilliseconds(500),
            clock.UtcNow);
    }

    [TestMethod]
    public void Create_NonZeroRoundTrip_CalibratesWithHalfRoundTrip()
    {
        // Arrange
        var timeProvider = new ControlledTimeProvider();
        var factory = new PamHttpDateClockFactory(timeProvider);

        // Act
        IClock clock = factory.Create(PortalDateHeader, TimeSpan.FromMilliseconds(240));

        // Assert
        Assert.AreEqual(
            new DateTimeOffset(2015, 10, 21, 7, 28, 0, TimeSpan.Zero).AddMilliseconds(620),
            clock.UtcNow);
    }

    [TestMethod]
    public void Create_PortalTimeOffsetBySixtySevenSeconds_UsesPortalTimeWithoutLocalClockFallback()
    {
        // Arrange
        var timeProvider = new ControlledTimeProvider();
        var factory = new PamHttpDateClockFactory(timeProvider);
        DateTimeOffset expected = new DateTimeOffset(2030, 1, 1, 0, 1, 6, TimeSpan.Zero)
            .AddMilliseconds(500);

        // Act
        IClock clock = factory.Create("Tue, 01 Jan 2030 00:01:06 GMT", TimeSpan.Zero);

        // Assert
        Assert.AreEqual(expected, clock.UtcNow);
    }

    [TestMethod]
    public void UtcNow_AfterCalibration_AdvancesUsingMonotonicTimeProvider()
    {
        // Arrange
        var timeProvider = new ControlledTimeProvider();
        var factory = new PamHttpDateClockFactory(timeProvider);
        IClock clock = factory.Create(PortalDateHeader, TimeSpan.Zero);

        // Act
        timeProvider.Advance(TimeSpan.FromSeconds(9));

        // Assert
        Assert.AreEqual(
            new DateTimeOffset(2015, 10, 21, 7, 28, 9, TimeSpan.Zero).AddMilliseconds(500),
            clock.UtcNow);
    }

    [TestMethod]
    public void Create_NullHttpDateHeader_ThrowsStablePamClockException()
    {
        // Arrange
        var factory = new PamHttpDateClockFactory(new ControlledTimeProvider());

        // Act
        PamClockException exception = Assert.ThrowsExactly<PamClockException>(
            () => factory.Create(null, TimeSpan.Zero));

        // Assert
        Assert.AreEqual(PamClockErrorCode.HttpDateUnavailableOrInvalid, exception.ErrorCode);
        Assert.AreEqual("El portal PAM no proporcionó una hora HTTP válida.", exception.Message);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not a date")]
    [DataRow("Wed, 21 Oct 2015 07:28:00")]
    public void Create_EmptyOrInvalidHttpDateHeader_ThrowsStablePamClockException(string header)
    {
        // Arrange
        var factory = new PamHttpDateClockFactory(new ControlledTimeProvider());

        // Act
        PamClockException exception = Assert.ThrowsExactly<PamClockException>(
            () => factory.Create(header, TimeSpan.Zero));

        // Assert
        Assert.AreEqual(PamClockErrorCode.HttpDateUnavailableOrInvalid, exception.ErrorCode);
        Assert.AreEqual("El portal PAM no proporcionó una hora HTTP válida.", exception.Message);
    }

    [TestMethod]
    public void Create_NegativeRoundTrip_ThrowsStablePamClockException()
    {
        // Arrange
        var factory = new PamHttpDateClockFactory(new ControlledTimeProvider());

        // Act
        PamClockException exception = Assert.ThrowsExactly<PamClockException>(
            () => factory.Create(PortalDateHeader, TimeSpan.FromMilliseconds(-1)));

        // Assert
        Assert.AreEqual(PamClockErrorCode.InvalidRoundTripTime, exception.ErrorCode);
        Assert.AreEqual(
            "La medición de tiempo de ida y vuelta del portal PAM no es válida.",
            exception.Message);
    }

    private sealed class ControlledTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return timestamp;
        }

        public override DateTimeOffset GetUtcNow()
        {
            throw new InvalidOperationException("No debe consultarse el reloj de pared local.");
        }

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }
}
