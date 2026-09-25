using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Requests;
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
    public void CaptureSnapshot_StartupTiming_KeepsSafeElapsedMilliseconds()
    {
        using var factory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = factory.CreateLogger("PamAutomation.App.App");

        logger.LogInformation(
            "Startup reached {Stage}. Elapsed={ElapsedMilliseconds}ms.",
            "MainWindowShown",
            125L);

        DiagnosticLogEvent captured = Assert.ContainsSingle(factory.CaptureSnapshot());
        Assert.AreEqual("MainWindowShown", captured.Properties["Stage"]);
        Assert.AreEqual("125", captured.Properties["ElapsedMilliseconds"]);
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

    [TestMethod]
    public void CaptureSnapshot_FormVerificationFailure_RecordsFieldNamesAndReadStateWithoutValues()
    {
        using var factory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = factory.CreateLogger("PamAutomation.App.ViewModels.MainWindowViewModel");
        var formFailure = new PamRequestFormVerificationException(
            PamRequestFormVerificationStage.AfterFill,
            PamRequestFormMismatchField.StartTime | PamRequestFormMismatchField.Reason,
            observationCount: 14,
            readFailureCount: 2,
            lastReadFailure: PamRequestFormReadFailure.Timeout,
            expected: new PamRequestFormDiagnosticValues("September 27, 2026", "00:00", "0", "8", "0", 14, "true"),
            observed: new PamRequestFormDiagnosticValues("September 27, 2026", "00:01", "0", "8", "0", 12, "false"));
        var submissionFailure = new PamRequestSubmissionException(
            PamRequestSubmissionErrorCode.PortalUnavailable,
            formFailure);

        logger.LogError(submissionFailure, "Application operation failed. Reason={ReasonValue}.", "private reason");

        DiagnosticLogEvent captured = Assert.ContainsSingle(factory.CaptureSnapshot());
        Assert.AreEqual("AfterFill", captured.Properties["FormVerificationStage"]);
        Assert.AreEqual("FieldMismatch", captured.Properties["FormFailureKind"]);
        Assert.AreEqual("StartTime.Reason", captured.Properties["FormMismatchFields"]);
        Assert.AreEqual("14", captured.Properties["FormObservationCount"]);
        Assert.AreEqual("2", captured.Properties["FormReadFailureCount"]);
        Assert.AreEqual("Timeout", captured.Properties["FormLastReadFailure"]);
        Assert.AreEqual("00:00", captured.Properties["FormExpectedStartTime"]);
        Assert.AreEqual("00:01", captured.Properties["FormObservedStartTime"]);
        Assert.AreEqual("12", captured.Properties["FormObservedReasonLength"]);
        Assert.AreEqual("false", captured.Properties["FormObservedApplicationSession"]);
        Assert.IsFalse(captured.Properties.ContainsKey("ReasonValue"));
        Assert.DoesNotContain("private reason", captured.MessageTemplate);
    }

    [TestMethod]
    public void CaptureSnapshot_UnreadableForm_DistinguishesReadFailureFromFieldMismatch()
    {
        using var factory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = factory.CreateLogger("PamAutomation.App.ViewModels.MainWindowViewModel");
        var formFailure = new PamRequestFormVerificationException(
            PamRequestFormVerificationStage.BeforeSubmit,
            PamRequestFormMismatchField.None,
            observationCount: 5,
            readFailureCount: 5,
            lastReadFailure: PamRequestFormReadFailure.InvalidOperation,
            expected: new PamRequestFormDiagnosticValues("September 27, 2026", "00:00", "0", "8", "0", 14, "true"),
            observed: null);

        logger.LogError(formFailure, "Form verification failed.");

        DiagnosticLogEvent captured = Assert.ContainsSingle(factory.CaptureSnapshot());
        Assert.AreEqual("BeforeSubmit", captured.Properties["FormVerificationStage"]);
        Assert.AreEqual("ReadUnstable", captured.Properties["FormFailureKind"]);
        Assert.AreEqual("None", captured.Properties["FormMismatchFields"]);
        Assert.AreEqual("5", captured.Properties["FormReadFailureCount"]);
        Assert.AreEqual("InvalidOperation", captured.Properties["FormLastReadFailure"]);
        Assert.AreEqual("NoCompleteRead", captured.Properties["FormObservedState"]);
    }

    [TestMethod]
    public void CaptureSnapshot_UnexpectedPortalValues_DoesNotExportArbitraryText()
    {
        using var factory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = factory.CreateLogger("PamAutomation.App.ViewModels.MainWindowViewModel");
        var formFailure = new PamRequestFormVerificationException(
            PamRequestFormVerificationStage.AfterFill,
            PamRequestFormMismatchField.StartDate | PamRequestFormMismatchField.StartTime,
            observationCount: 1,
            readFailureCount: 0,
            lastReadFailure: PamRequestFormReadFailure.None,
            expected: new PamRequestFormDiagnosticValues("September 27, 2026", "00:00", "0", "8", "0", 5, "true"),
            observed: new PamRequestFormDiagnosticValues("secret-token", "secret-password", "secret", "8", "0", 9, "secret"));

        logger.LogError(formFailure, "Form verification failed.");

        DiagnosticLogEvent captured = Assert.ContainsSingle(factory.CaptureSnapshot());
        Assert.AreEqual("UnexpectedFormat", captured.Properties["FormObservedStartDate"]);
        Assert.AreEqual("UnexpectedFormat", captured.Properties["FormObservedStartTime"]);
        Assert.AreEqual("UnexpectedFormat", captured.Properties["FormObservedDays"]);
        Assert.AreEqual("UnavailableOrUnexpected", captured.Properties["FormObservedApplicationSession"]);
        Assert.DoesNotContain("secret", string.Join('|', captured.Properties.Values));
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
