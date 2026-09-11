using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Infrastructure.Diagnostics;

namespace PamAutomation.Tests.Infrastructure.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class DevelopmentFileLoggerFactoryTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "PamAutomation.Tests",
        Guid.NewGuid().ToString("N"));

    [TestMethod]
    public void LogInformation_SafeEvent_WritesDailyStructuredFile()
    {
        // Arrange
        DateTimeOffset now = new(2026, 9, 11, 15, 30, 0, TimeSpan.Zero);
        using var factory = new DevelopmentFileLoggerFactory(directory, new FixedTimeProvider(now));
        ILogger logger = factory.CreateLogger("PamAutomation.Tests.Component");

        // Act
        logger.LogInformation("Diagnostic operation completed. Count={Count}.", 2);

        // Assert
        string content = File.ReadAllText(Path.Combine(directory, "pam-automation-2026-09-11.log"));
        Assert.Contains("|INFO|PamAutomation.Tests.Component|", content);
        Assert.Contains("Diagnostic operation completed. Count=2.", content);
    }

    [TestMethod]
    public void LogError_ExceptionWithSensitiveMessage_OmitsExceptionMessage()
    {
        // Arrange
        using var factory = new DevelopmentFileLoggerFactory(directory);
        ILogger logger = factory.CreateLogger("PamAutomation.Tests.Component");
        var exception = CaptureExceptionWithStackTrace();

        // Act
        logger.LogError(exception, "Safe operation failure.");

        // Assert
        string content = File.ReadAllText(Directory.GetFiles(directory, "pam-automation-*.log").Single());
        Assert.Contains("ExceptionType=System.InvalidOperationException", content);
        Assert.Contains("HResult=0x", content);
        Assert.DoesNotContain("sensitive diagnostic value", content);
    }

    [TestMethod]
    public void Constructor_ExpiredDailyLog_DeletesOnlyExpiredLogFiles()
    {
        // Arrange
        Directory.CreateDirectory(directory);
        string expired = Path.Combine(directory, "pam-automation-2026-09-08.log");
        string retained = Path.Combine(directory, "pam-automation-2026-09-10.log");
        string unrelated = Path.Combine(directory, "keep.txt");
        File.WriteAllText(expired, "expired");
        File.WriteAllText(retained, "retained");
        File.WriteAllText(unrelated, "unrelated");
        File.SetLastWriteTimeUtc(expired, new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(retained, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
        DateTimeOffset now = new(2026, 9, 11, 15, 30, 0, TimeSpan.Zero);

        // Act
        using var factory = new DevelopmentFileLoggerFactory(directory, new FixedTimeProvider(now));

        // Assert
        Assert.IsFalse(File.Exists(expired));
        Assert.IsTrue(File.Exists(retained));
        Assert.IsTrue(File.Exists(unrelated));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static Exception CaptureExceptionWithStackTrace()
    {
        try
        {
            throw new InvalidOperationException("sensitive diagnostic value");
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
