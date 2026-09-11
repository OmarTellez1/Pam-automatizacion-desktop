using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Infrastructure.Diagnostics;

namespace PamAutomation.Tests.Infrastructure.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class RuntimeDiagnosticLoggerFactoryTests
{
    [TestMethod]
    public void CaptureSnapshot_StructuredSensitiveValues_KeepsOnlyAllowlistedProperties()
    {
        var now = new DateTimeOffset(2026, 9, 11, 16, 0, 0, TimeSpan.Zero);
        using var factory = new RuntimeDiagnosticLoggerFactory(timeProvider: new FixedTimeProvider(now));
        ILogger logger = factory.CreateLogger("PamAutomation.Tests.Component");

        logger.LogInformation(
            "Operation for {Email} used {RequestIdCount} identifiers at {Stage}.",
            "private@example.test",
            2,
            "PendingRead");

        DiagnosticLogEvent captured = Assert.ContainsSingle(factory.CaptureSnapshot());
        Assert.AreEqual(now, captured.TimestampUtc);
        Assert.AreEqual("Operation for {Email} used {RequestIdCount} identifiers at {Stage}.", captured.MessageTemplate);
        Assert.DoesNotContain("private@example.test", captured.MessageTemplate);
        Assert.AreEqual("2", captured.Properties["RequestIdCount"]);
        Assert.AreEqual("PendingRead", captured.Properties["Stage"]);
        Assert.IsFalse(captured.Properties.ContainsKey("Email"));
    }

    [TestMethod]
    public void CaptureSnapshot_EventsExceedCapacity_DropsOldestEvents()
    {
        using var factory = new RuntimeDiagnosticLoggerFactory(capacity: 2);
        ILogger logger = factory.CreateLogger("PamAutomation.Tests.Component");

        logger.LogInformation("First event.");
        logger.LogInformation("Second event.");
        logger.LogInformation("Third event.");

        IReadOnlyList<DiagnosticLogEvent> captured = factory.CaptureSnapshot();
        Assert.HasCount(2, captured);
        Assert.AreEqual("Second event.", captured[0].MessageTemplate);
        Assert.AreEqual("Third event.", captured[1].MessageTemplate);
    }

    [TestMethod]
    public void CaptureSnapshot_Exception_OmitsMessageAndSourcePaths()
    {
        using var factory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = factory.CreateLogger("PamAutomation.Tests.Component");
        Exception exception = CaptureException();

        logger.LogError(exception, "Safe failure.");

        DiagnosticExceptionInfo metadata = Assert.ContainsSingle(
            Assert.ContainsSingle(factory.CaptureSnapshot()).Exceptions);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, metadata.Type);
        Assert.DoesNotContain("private-exception-value", metadata.StackTrace ?? string.Empty);
        Assert.DoesNotContain(":line", metadata.StackTrace ?? string.Empty);
        Assert.DoesNotContain("C:\\", metadata.StackTrace ?? string.Empty);
    }

    private static Exception CaptureException()
    {
        try
        {
            throw new InvalidOperationException("private-exception-value");
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
