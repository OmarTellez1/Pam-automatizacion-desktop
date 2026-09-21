using PamAutomation.Core.Diagnostics;

namespace PamAutomation.Core.Interfaces.Diagnostics;

/// <summary>Exports a diagnostic package only after an explicit user request.</summary>
public interface IDiagnosticExportService
{
    /// <summary>Exports the current diagnostic snapshot to the exact path selected by the user.</summary>
    /// <param name="destinationPath">Final ZIP path selected by the user.</param>
    /// <param name="context">Application and execution state approved for export.</param>
    /// <param name="cancellationToken">Cancels package creation before publication.</param>
    Task ExportAsync(
        string destinationPath,
        DiagnosticExportContext context,
        CancellationToken cancellationToken = default);
}
