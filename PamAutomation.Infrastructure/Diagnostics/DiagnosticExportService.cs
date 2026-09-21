using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Interfaces.Diagnostics;

namespace PamAutomation.Infrastructure.Diagnostics;

/// <summary>Creates an explicit ZIP diagnostic package without retaining operational files automatically.</summary>
public sealed class DiagnosticExportService : IDiagnosticExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IDiagnosticEventSource eventSource;
    private readonly IPendingReconciliationDiagnosticSource? pendingDiagnosticSource;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes the exporter from a safe in-memory event source.</summary>
    /// <param name="eventSource">Source of already filtered diagnostic events.</param>
    /// <param name="timeProvider">Clock used for package metadata.</param>
    /// <param name="pendingDiagnosticSource">Optional source of explicitly authorized Pending details.</param>
    public DiagnosticExportService(
        IDiagnosticEventSource eventSource,
        TimeProvider? timeProvider = null,
        IPendingReconciliationDiagnosticSource? pendingDiagnosticSource = null)
    {
        this.eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.pendingDiagnosticSource = pendingDiagnosticSource;
    }

    /// <inheritdoc />
    public async Task ExportAsync(
        string destinationPath,
        DiagnosticExportContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(context);
        string fullDestinationPath = Path.GetFullPath(destinationPath);
        if (!string.Equals(Path.GetExtension(fullDestinationPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("El diagnóstico debe exportarse como un archivo ZIP.", nameof(destinationPath));
        }

        string? destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException("La carpeta seleccionada ya no está disponible.");
        }

        string temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");
        IReadOnlyList<DiagnosticLogEvent> events = eventSource.CaptureSnapshot();
        PendingReconciliationDiagnostic? pendingDiagnostic = pendingDiagnosticSource?.CaptureSnapshot();
        DateTimeOffset generatedAtUtc = timeProvider.GetUtcNow();
        string[] files = pendingDiagnostic is null
            ? ["manifest.json", "environment.json", "execution-summary.json", "diagnostic.log"]
            : [
                "manifest.json",
                "environment.json",
                "execution-summary.json",
                "diagnostic.log",
                "pending-reconciliation.json",
            ];
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var file = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
                await WriteJsonEntryAsync(
                    archive,
                    "manifest.json",
                    new DiagnosticManifest(pendingDiagnostic is null ? 1 : 2, generatedAtUtc, events.Count, files),
                    cancellationToken).ConfigureAwait(false);
                await WriteJsonEntryAsync(
                    archive,
                    "environment.json",
                    new DiagnosticEnvironment(
                        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown",
                        Assembly.GetEntryAssembly()?
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                            .InformationalVersion ?? "Unknown",
                        Assembly.GetEntryAssembly()?.ManifestModule.ModuleVersionId.ToString("D") ?? "Unknown",
                        Environment.Version.ToString(),
                        RuntimeInformation.OSDescription,
                        RuntimeInformation.ProcessArchitecture.ToString(),
                        generatedAtUtc,
                        generatedAtUtc.ToOffset(TimeSpan.FromHours(-5))),
                    cancellationToken).ConfigureAwait(false);
                await WriteJsonEntryAsync(
                    archive,
                    "execution-summary.json",
                    new DiagnosticApplicationState(
                        context.CredentialsConfigured,
                        context.RequestConfigured,
                        context.MailConfigured,
                        context.LastExecution),
                    cancellationToken).ConfigureAwait(false);
                await WriteLogEntryAsync(archive, events, cancellationToken).ConfigureAwait(false);
                if (pendingDiagnostic is not null)
                {
                    await WriteJsonEntryAsync(
                        archive,
                        "pending-reconciliation.json",
                        pendingDiagnostic,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullDestinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteLogEntryAsync(
        ZipArchive archive,
        IReadOnlyList<DiagnosticLogEvent> events,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry("diagnostic.log", CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false);
        foreach (DiagnosticLogEvent diagnosticEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = new StringBuilder(256)
                .Append(diagnosticEvent.TimestampUtc.ToString("O", CultureInfo.InvariantCulture))
                .Append('|').Append(diagnosticEvent.Level.ToString().ToUpperInvariant())
                .Append('|').Append(diagnosticEvent.Category)
                .Append("|EventId=").Append(diagnosticEvent.EventId)
                .Append('|').Append(diagnosticEvent.MessageTemplate);
            foreach ((string key, string value) in diagnosticEvent.Properties.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                line.Append('|').Append(key).Append('=').Append(value);
            }
            foreach (DiagnosticExceptionInfo exception in diagnosticEvent.Exceptions)
            {
                line.Append("|ExceptionType=").Append(exception.Type)
                    .Append("|HResult=").Append(exception.HResult);
                if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                {
                    line.Append("|StackTrace=")
                        .Append(exception.StackTrace.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/'));
                }
            }
            await writer.WriteLineAsync(line, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record DiagnosticManifest(
        int SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        int EventCount,
        IReadOnlyList<string> Files);

    private sealed record DiagnosticEnvironment(
        string ApplicationVersion,
        string ApplicationInformationalVersion,
        string ApplicationModuleVersionId,
        string DotNetVersion,
        string OperatingSystem,
        string ProcessArchitecture,
        DateTimeOffset GeneratedAtUtc,
        DateTimeOffset GeneratedAtEcuador);

    private sealed record DiagnosticApplicationState(
        bool CredentialsConfigured,
        bool RequestConfigured,
        bool MailConfigured,
        DiagnosticExecutionSummary? LastExecution);
}
