using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Requests;

namespace PamAutomation.Infrastructure.Diagnostics;

/// <summary>
/// Keeps a bounded, non-persistent diagnostic history in memory and mirrors events to the development logger when enabled.
/// </summary>
public sealed partial class RuntimeDiagnosticLoggerFactory : ILoggerFactory, IDiagnosticEventSource
{
    private const int DefaultCapacity = 500;
    private static readonly HashSet<string> AllowedPropertyNames = new(StringComparer.Ordinal)
    {
        "Mode", "Stage", "Status", "FinalAction", "Complete", "IsComplete", "IsTerminating", "Terminating",
        "ElapsedMilliseconds", "FormVerificationStage", "FormFailureKind",
        "FormMismatchFields", "FormLastReadFailure"
    };

    private readonly object gate = new();
    private readonly Queue<DiagnosticLogEvent> events = new();
    private readonly ILoggerFactory developmentFactory;
    private readonly TimeProvider timeProvider;
    private readonly int capacity;
    private bool disposed;

    /// <summary>Initializes an in-memory diagnostic logger.</summary>
    /// <param name="developmentFactory">Optional logger used for repository-only development files.</param>
    /// <param name="timeProvider">Clock used to timestamp captured events.</param>
    /// <param name="capacity">Maximum number of events retained in memory.</param>
    public RuntimeDiagnosticLoggerFactory(
        ILoggerFactory? developmentFactory = null,
        TimeProvider? timeProvider = null,
        int capacity = DefaultCapacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.developmentFactory = developmentFactory ?? NullLoggerFactory.Instance;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.capacity = capacity;
    }

    /// <summary>Creates the standard runtime logger and enables repository logs only in development.</summary>
    /// <returns>A logger factory whose in-memory events can be exported explicitly.</returns>
    public static RuntimeDiagnosticLoggerFactory CreateForCurrentProcess() =>
        new(DevelopmentFileLoggerFactory.CreateForCurrentProcess());

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new RuntimeDiagnosticLogger(this, categoryName, developmentFactory.CreateLogger(categoryName));
    }

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(provider);
        throw new NotSupportedException("La fábrica de diagnóstico administra únicamente sus destinos seguros.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DiagnosticLogEvent> CaptureSnapshot()
    {
        lock (gate)
        {
            return events.ToArray();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        developmentFactory.Dispose();
    }

    private void Capture<TState>(
        LogLevel level,
        string category,
        EventId eventId,
        TState state,
        Exception? exception)
    {
        if (level == LogLevel.None) return;
        string template = "Unstructured diagnostic event omitted.";
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            foreach (KeyValuePair<string, object?> value in values)
            {
                if (string.Equals(value.Key, "{OriginalFormat}", StringComparison.Ordinal))
                {
                    template = SanitizeTemplate(Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty);
                }
                else if (IsAllowedProperty(value.Key, value.Value, out string? safeValue))
                {
                    properties[value.Key] = safeValue;
                }
            }
        }

        var exceptionMetadata = new List<DiagnosticExceptionInfo>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PamCalendarSelectionException calendarFailure)
            {
                properties["CalendarFailure"] = calendarFailure.Failure.ToString();
                properties["CalendarTargetDate"] = calendarFailure.TargetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                properties["CalendarObservedMonth"] = calendarFailure.ObservedMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture) ?? "Unavailable";
                properties["CalendarNavigationCount"] = calendarFailure.NavigationCount.ToString(CultureInfo.InvariantCulture);
            }
            if (current is PamRequestFormVerificationException formFailure)
            {
                CaptureFormVerification(properties, formFailure);
            }

            string? stackTrace = string.IsNullOrWhiteSpace(current.StackTrace)
                ? null
                : new StackTrace(current, false).ToString().Trim();
            exceptionMetadata.Add(new DiagnosticExceptionInfo(
                current.GetType().FullName ?? current.GetType().Name,
                $"0x{current.HResult:X8}",
                stackTrace));
        }

        var diagnosticEvent = new DiagnosticLogEvent(
            timeProvider.GetUtcNow(),
            level,
            category,
            eventId.Id,
            template,
            properties,
            exceptionMetadata);
        lock (gate)
        {
            events.Enqueue(diagnosticEvent);
            while (events.Count > capacity) events.Dequeue();
        }
    }

    private static void CaptureFormVerification(
        IDictionary<string, string> properties,
        PamRequestFormVerificationException failure)
    {
        string mismatchFields = string.Join(
            '.',
            Enum.GetValues<PamRequestFormMismatchField>()
                .Where(field => field != PamRequestFormMismatchField.None &&
                    failure.MismatchFields.HasFlag(field)));
        AddAllowed(properties, "FormVerificationStage", failure.Stage);
        AddAllowed(properties, "FormFailureKind", mismatchFields.Length == 0 ? "ReadUnstable" : "FieldMismatch");
        AddAllowed(properties, "FormMismatchFields", mismatchFields.Length == 0 ? "None" : mismatchFields);
        AddAllowed(properties, "FormObservationCount", failure.ObservationCount);
        AddAllowed(properties, "FormReadFailureCount", failure.ReadFailureCount);
        AddAllowed(properties, "FormLastReadFailure", failure.LastReadFailure);
        CaptureFormValues(properties, "Expected", failure.Expected);
        if (failure.Observed is not null)
            CaptureFormValues(properties, "Observed", failure.Observed);
        else
            properties["FormObservedState"] = "NoCompleteRead";
    }

    private static void CaptureFormValues(
        IDictionary<string, string> properties,
        string side,
        PamRequestFormDiagnosticValues values)
    {
        properties[$"Form{side}StartDate"] = SafeFormValue(values.StartDate, FormDateRegex());
        properties[$"Form{side}StartTime"] = SafeFormValue(values.StartTime, FormTimeRegex());
        properties[$"Form{side}Days"] = SafeFormValue(values.Days, FormDurationRegex());
        properties[$"Form{side}Hours"] = SafeFormValue(values.Hours, FormDurationRegex());
        properties[$"Form{side}Minutes"] = SafeFormValue(values.Minutes, FormDurationRegex());
        if (values.ReasonLength is >= 0)
            properties[$"Form{side}ReasonLength"] = values.ReasonLength.Value.ToString(CultureInfo.InvariantCulture);
        properties[$"Form{side}ApplicationSession"] = values.ApplicationSession is "true" or "false"
            ? values.ApplicationSession
            : "UnavailableOrUnexpected";
    }

    private static string SafeFormValue(string? value, Regex allowedPattern) =>
        value is null ? "Unavailable" : allowedPattern.IsMatch(value) ? value : "UnexpectedFormat";

    private static void AddAllowed(IDictionary<string, string> properties, string name, object value)
    {
        if (IsAllowedProperty(name, value, out string safeValue)) properties[name] = safeValue;
    }

    private static bool IsAllowedProperty(string name, object? value, out string safeValue)
    {
        safeValue = string.Empty;
        if (!AllowedPropertyNames.Contains(name) && !name.EndsWith("Count", StringComparison.Ordinal)) return false;
        if (value is null) return false;

        if (value is bool or byte or sbyte or short or ushort or int or uint or long or ulong)
        {
            safeValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }

        string candidate = value is Enum
            ? value.ToString() ?? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (!SafeIdentifierRegex().IsMatch(candidate)) return false;
        safeValue = candidate;
        return true;
    }

    private static string SanitizeTemplate(string template)
    {
        string sanitized = template.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        sanitized = EmailRegex().Replace(sanitized, "[REDACTED_EMAIL]");
        sanitized = WindowsPathRegex().Replace(sanitized, "[REDACTED_PATH]");
        sanitized = LongTokenRegex().Replace(sanitized, "[REDACTED_VALUE]");
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex(@"^(?:[A-Za-z]{3,9} [0-3]?\d, \d{4}|\d{1,2}[/.-]\d{1,2}[/.-]\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex FormDateRegex();

    [GeneratedRegex(@"^(?:[01]?\d|2[0-3]):[0-5]\d(?: (?:AM|PM))?$", RegexOptions.CultureInvariant)]
    private static partial Regex FormTimeRegex();

    [GeneratedRegex(@"^\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormDurationRegex();

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"[A-Za-z]:\\[^\r\n|]*", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"\b[A-Z2-7]{16,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LongTokenRegex();

    private sealed class RuntimeDiagnosticLogger(
        RuntimeDiagnosticLoggerFactory owner,
        string category,
        ILogger developmentLogger) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            developmentLogger.BeginScope(state) ?? NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            ArgumentNullException.ThrowIfNull(formatter);
            owner.Capture(logLevel, category, eventId, state, exception);
            if (developmentLogger.IsEnabled(logLevel))
            {
                developmentLogger.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
