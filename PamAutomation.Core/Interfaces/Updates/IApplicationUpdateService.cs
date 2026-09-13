using PamAutomation.Core.Updates;

namespace PamAutomation.Core.Interfaces.Updates;

/// <summary>Checks whether a newer portable application version is available.</summary>
public interface IApplicationUpdateService
{
    /// <summary>Reads the published version and compares it with the running version.</summary>
    /// <param name="currentVersion">Version embedded in the running executable.</param>
    /// <param name="cancellationToken">Token used to cancel the remote check.</param>
    /// <returns>The validated update availability result.</returns>
    Task<ApplicationUpdateResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default);
}
