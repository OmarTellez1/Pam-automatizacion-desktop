using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Diagnostics;
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
