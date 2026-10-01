using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Requests;
using PamAutomation.Infrastructure.Diagnostics;

namespace PamAutomation.Tests.Infrastructure.Diagnostics;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticExportServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "PamAutomation.Tests",
        Guid.NewGuid().ToString("N"));

    [TestMethod]
    public async Task ExportAsync_SafeContext_CreatesExpectedPackageWithoutIdentifiers()
    {
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "diagnostic.zip");
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = loggerFactory.CreateLogger("PamAutomation.Tests.Component");
        logger.LogInformation(
            "Execution completed. RequestIdCount={RequestIdCount}, Email={Email}.",
            2,
            "private@example.test");
        var service = new DiagnosticExportService(loggerFactory, new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 11, 16, 30, 0, TimeSpan.Zero)));
        var context = new DiagnosticExportContext(
            true,
            true,
            false,
            new DiagnosticExecutionSummary("Real", DateTimeOffset.UtcNow, "DisplayRequestIds", 2, 2, 0, 2, null));

        await service.ExportAsync(destination, context);

        Assert.IsTrue(File.Exists(destination));
        using ZipArchive archive = ZipFile.OpenRead(destination);
        CollectionAssert.AreEquivalent(
            new[] { "manifest.json", "environment.json", "execution-summary.json", "diagnostic.log" },
            archive.Entries.Select(static entry => entry.FullName).ToArray());
        string allContent = string.Join("\n", archive.Entries.Select(ReadEntry));
        Assert.Contains("\"requestIdCount\": 2", allContent);
        Assert.DoesNotContain("private@example.test", allContent);
        Assert.DoesNotContain("8001", allContent);
        Assert.DoesNotContain("password", allContent, StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(Directory.EnumerateFiles(directory, "*.tmp").Any());
    }

    [TestMethod]
    public async Task ExportAsync_FormVerificationFailure_PreservesSafeStructuredDetails()
    {
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "form-diagnostic.zip");
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        ILogger logger = loggerFactory.CreateLogger("PamAutomation.App.ViewModels.MainWindowViewModel");
        var formFailure = new PamRequestFormVerificationException(
            PamRequestFormVerificationStage.BeforeSubmit,
            PamRequestFormMismatchField.StartDate | PamRequestFormMismatchField.Reason,
            observationCount: 4,
            readFailureCount: 1,
            lastReadFailure: PamRequestFormReadFailure.Timeout,
            expected: new PamRequestFormDiagnosticValues("September 27, 2026", "00:00", "0", "8", "0", 14, "true"),
            observed: new PamRequestFormDiagnosticValues("September 26, 2026", "00:00", "0", "8", "0", 21, "true"));
        logger.LogError(
            new PamRequestSubmissionException(PamRequestSubmissionErrorCode.PortalUnavailable, formFailure),
            "Application operation failed. ReasonValue={ReasonValue}.",
            "private reason");
        var service = new DiagnosticExportService(loggerFactory);

        await service.ExportAsync(
            destination,
            new DiagnosticExportContext(true, true, false, null));

        using ZipArchive archive = ZipFile.OpenRead(destination);
        ZipArchiveEntry? logEntry = archive.GetEntry("diagnostic.log");
        Assert.IsNotNull(logEntry);
        string log = ReadEntry(logEntry);
        Assert.Contains("FormVerificationStage=BeforeSubmit", log);
        Assert.Contains("FormMismatchFields=StartDate.Reason", log);
        Assert.Contains("FormReadFailureCount=1", log);
        Assert.Contains("FormLastReadFailure=Timeout", log);
        Assert.Contains("FormExpectedStartDate=September 27, 2026", log);
        Assert.Contains("FormObservedStartDate=September 26, 2026", log);
        Assert.Contains("FormObservedReasonLength=21", log);
        Assert.DoesNotContain("private reason", log);
        Assert.DoesNotContain("ReasonValue=private", log);
    }

    [TestMethod]
    public async Task ExportAsync_PendingTraceAvailable_IncludesAuthorizedDetailedJson()
    {
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "diagnostic.zip");
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        var pendingStore = new InMemoryPendingReconciliationDiagnosticStore();
        var row = new PendingRequestDiagnostic(
            "154359",
            "SYSTEM-A",
            "Application A",
            "account-a",
            new DateOnly(2026, 9, 21),
            new TimeOnly(16, 30),
            new DateOnly(2026, 9, 21),
            new TimeOnly(17, 30),
            "Solicitud autorizada");
        pendingStore.Begin(new PendingReconciliationDiagnostic(
            2,
            "execution-1",
            DateTimeOffset.UtcNow,
            "UseExistingRequestIds",
            1,
            [new PendingExpectedRequestDiagnostic(
                row.System,
                row.Application,
                row.Account,
                row.StartDate,
                row.StartTime,
                row.ExpiresDate,
                row.ExpiresTime,
                row.Description)],
            new PendingSnapshotDiagnostic(DateTimeOffset.UtcNow, 1, 1, [row]),
            1,
            [],
            [],
            null));
        pendingStore.RecordAttempt(new PendingComparisonAttemptDiagnostic(
            1,
            DateTimeOffset.UtcNow,
            2,
            2,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            "AmbiguousIdentity",
            [new PendingGroupMultiplicityDiagnostic("SYSTEM-A", "account-a", 1, 2)],
            [row, row with { RequestId = "154360" }],
            [row with { RequestId = "154360" }]));
        pendingStore.RecordRecovery(new PendingReadRecoveryDiagnostic(
            DateTimeOffset.UtcNow,
            1,
            1,
            1,
            "ReloadPendingView",
            "PortalUnavailable"));
        pendingStore.RecordFailure("AmbiguousIdentity");
        pendingStore.RecordRead(new PendingReadAttemptDiagnostic(
            DateTimeOffset.UtcNow, "Reconciliation", 1, 1, 1, "SnapshotUnstable")
        {
            ElapsedMilliseconds = 5010,
            Browser = new PendingBrowserReadDiagnostic("NextPage", 1, 2, 30, 25, false),
        });
        var service = new DiagnosticExportService(
            loggerFactory,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 21, 21, 0, 0, TimeSpan.Zero)),
            pendingStore);

        await service.ExportAsync(
            destination,
            new DiagnosticExportContext(true, true, true, null));

        using ZipArchive archive = ZipFile.OpenRead(destination);
        Assert.Contains("pending-reconciliation.json", archive.Entries.Select(static entry => entry.FullName));
        ZipArchiveEntry? detailEntry = archive.GetEntry("pending-reconciliation.json");
        Assert.IsNotNull(detailEntry);
        string detail = ReadEntry(detailEntry);
        Assert.Contains("154359", detail);
        Assert.Contains("SYSTEM-A", detail);
        Assert.Contains("account-a", detail);
        Assert.Contains("Solicitud autorizada", detail);
        Assert.Contains("AmbiguousIdentity", detail);
        Assert.Contains("ReloadPendingView", detail);
        Assert.Contains("PortalUnavailable", detail);
        Assert.Contains("\"operation\": \"NextPage\"", detail);
        Assert.Contains("\"totalItems\": 30", detail);
        Assert.Contains("\"rowsRead\": 25", detail);
        Assert.Contains("\"nextPageEnabled\": false", detail);
        Assert.Contains("\"elapsedMilliseconds\": 5010", detail);
        Assert.DoesNotContain("password", detail, StringComparison.OrdinalIgnoreCase);
        ZipArchiveEntry? manifestEntry = archive.GetEntry("manifest.json");
        Assert.IsNotNull(manifestEntry);
        string manifest = ReadEntry(manifestEntry);
        Assert.Contains("\"schemaVersion\": 2", manifest);
    }

    [TestMethod]
    public async Task ExportAsync_Cancelled_DoesNotLeaveDestinationOrTemporaryFile()
    {
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "diagnostic.zip");
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        var service = new DiagnosticExportService(loggerFactory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.ExportAsync(destination, new DiagnosticExportContext(false, false, false, null), cancellation.Token));

        Assert.IsFalse(File.Exists(destination));
        Assert.IsFalse(Directory.EnumerateFiles(directory, "*.tmp").Any());
    }

    [TestMethod]
    public async Task ExportAsync_NonZipDestination_RejectsPathBeforeWriting()
    {
        Directory.CreateDirectory(directory);
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        var service = new DiagnosticExportService(loggerFactory);
        string destination = Path.Combine(directory, "diagnostic.json");

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.ExportAsync(destination, new DiagnosticExportContext(false, false, false, null)));

        Assert.IsFalse(File.Exists(destination));
    }

    [TestMethod]
    public async Task ExportAsync_FailedAttemptAfterSuccess_PreservesCorrelationHistoryAndComponentIdentity()
    {
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "attempt.zip");
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        var attempt = new PamExecutionAttempt(6);
        attempt.RecordBrowserVersions(new Version(140, 0, 0, 1), new Version(1, 55, 0));
        using IDisposable scope = attempt.Activate();
        attempt.RecordStage("Form", 3);
        attempt.RecordCounts(2, 0, notAttempted: 3, failedBeforeSubmission: 1);
        var calendar = new PamCalendarSelectionException(PamCalendarFailure.MonthUnreadable, new(2026, 11, 2), null, 0,
            new InvalidOperationException("secret-portal-content"), new PamCalendarDiagnostic(attempt.Capture().AttemptId, 3, 6, 40, 2, 0,
                [new(PamCalendarStage.ReadInitialMonth, PamCalendarReadCondition.HeaderIncomplete, 8100, 36900, 1, 1, 0, null, null, null, true)]));
        loggerFactory.CreateLogger("Test").LogError(calendar, "Form failed.");
        attempt.Finish("Partial");
        var success = new DiagnosticExecutionSummary("Real", DateTimeOffset.UtcNow.AddMinutes(-10), "DisplayRequestIds", 9, 9, 0, 9, null);
        await new DiagnosticExportService(loggerFactory).ExportAsync(destination,
            new DiagnosticExportContext(true, true, false, success) { LastAttempt = attempt.Capture() });
        using ZipArchive archive = ZipFile.OpenRead(destination);
        string log = ReadEntry(archive.GetEntry("diagnostic.log")!);
        Assert.Contains(attempt.Capture().AttemptId.ToString("D"), log);
        Assert.Contains("HeaderIncomplete", log);
        Assert.Contains("1:1:0:Unknown", log);
        Assert.Contains("CalendarFormOrdinal=3", log);
        Assert.DoesNotContain("secret-portal-content", log);
        using var state = System.Text.Json.JsonDocument.Parse(ReadEntry(archive.GetEntry("execution-summary.json")!));
        Assert.AreEqual(9, state.RootElement.GetProperty("lastExecution").GetProperty("requestIdCount").GetInt32());
        Assert.AreEqual("Partial", state.RootElement.GetProperty("lastAttempt").GetProperty("status").GetString());
        Assert.AreEqual("140.0.0.1", state.RootElement.GetProperty("lastAttempt").GetProperty("edgeVersion").GetString());
        Assert.AreEqual("1.55.0", state.RootElement.GetProperty("lastAttempt").GetProperty("playwrightVersion").GetString());
        Assert.AreEqual(2, state.RootElement.GetProperty("lastAttempt").GetProperty("acceptedCount").GetInt32());
        Assert.AreEqual(1, state.RootElement.GetProperty("lastAttempt").GetProperty("failedBeforeSubmissionCount").GetInt32());
        using var environment = System.Text.Json.JsonDocument.Parse(ReadEntry(archive.GetEntry("environment.json")!));
        var components = environment.RootElement.GetProperty("components").EnumerateArray().ToArray();
        Assert.IsTrue(components.Any(component => component.GetProperty("name").GetString() == "PamAutomation.Core"));
        Assert.IsTrue(components.Any(component => component.GetProperty("name").GetString() == "PamAutomation.Infrastructure"));
        foreach (var component in components) Assert.IsTrue(Guid.TryParse(component.GetProperty("moduleVersionId").GetString(), out _));
    }


    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
