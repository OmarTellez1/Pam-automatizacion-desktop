using PamAutomation.Core.Diagnostics;

namespace PamAutomation.Core.Interfaces.Diagnostics;

/// <summary>Exports an explicitly requested, non-sensitive diagnostic package.</summary>
public interface IDiagnosticExportService
{
    /// <summary>Exports the current diagnostic snapshot to the exact path selected by the user.</summary>
    /// <param name="destinationPath">Final ZIP path selected by the user.</param>
    /// <param name="context">Allowlisted application and execution state.</param>
    /// <param name="cancellationToken">Cancels package creation before publication.</param>
    Task ExportAsync(
        string destinationPath,
        DiagnosticExportContext context,
        CancellationToken cancellationToken = default);
}
