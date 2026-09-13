using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Interfaces.Configuration;

/// <summary>
/// Persists the non-sensitive application configuration.
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>
    /// Loads the current configuration or safe defaults when it has not been created.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read operation.</param>
    /// <returns>The stored configuration or a new default configuration.</returns>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the current non-sensitive configuration.
    /// </summary>
    /// <param name="settings">Configuration to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    /// <returns>A task that completes when the configuration is durable.</returns>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
