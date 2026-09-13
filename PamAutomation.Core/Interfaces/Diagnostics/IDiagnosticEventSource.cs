using PamAutomation.Core.Diagnostics;

namespace PamAutomation.Core.Interfaces.Diagnostics;

/// <summary>Provides a point-in-time copy of safe diagnostic events held in memory.</summary>
public interface IDiagnosticEventSource
{
    /// <summary>Creates an immutable snapshot of the current in-memory events.</summary>
    /// <returns>Events ordered from oldest to newest.</returns>
    IReadOnlyList<DiagnosticLogEvent> CaptureSnapshot();
}
