using Microsoft.Extensions.Logging;

namespace PamAutomation.Core.Diagnostics;

/// <summary>Represents one allowlisted, in-memory diagnostic event.</summary>
public sealed record DiagnosticLogEvent
{
    /// <summary>Initializes a safe diagnostic event.</summary>
    public DiagnosticLogEvent(
        DateTimeOffset timestampUtc,
        LogLevel level,
        string category,
        int eventId,
        string messageTemplate,
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<DiagnosticExceptionInfo> exceptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(messageTemplate);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(exceptions);

        TimestampUtc = timestampUtc;
        Level = level;
        Category = category;
        EventId = eventId;
        MessageTemplate = messageTemplate;
        Properties = new Dictionary<string, string>(properties, StringComparer.Ordinal);
        Exceptions = exceptions.ToArray();
    }

    /// <summary>Gets the UTC event timestamp.</summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>Gets the event severity.</summary>
    public LogLevel Level { get; }

    /// <summary>Gets the logger category.</summary>
    public string Category { get; }

    /// <summary>Gets the numeric event identifier.</summary>
    public int EventId { get; }

    /// <summary>Gets the message template without formatted parameter values.</summary>
    public string MessageTemplate { get; }

    /// <summary>Gets allowlisted structured properties.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>Gets non-sensitive exception metadata.</summary>
    public IReadOnlyList<DiagnosticExceptionInfo> Exceptions { get; }
}
