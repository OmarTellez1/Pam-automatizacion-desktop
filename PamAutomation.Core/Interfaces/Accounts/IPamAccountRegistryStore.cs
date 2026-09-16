using PamAutomation.Core.Accounts;

namespace PamAutomation.Core.Interfaces.Accounts;

/// <summary>Persists the non-sensitive PAM-account index.</summary>
public interface IPamAccountRegistryStore
{
    Task<PamAccountRegistryState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PamAccountRegistryState state, CancellationToken cancellationToken = default);
}
