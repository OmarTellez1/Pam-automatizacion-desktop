using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Interfaces.Configuration;

/// <summary>Persists the Outlook classic account for the current Windows user.</summary>
public interface IOutlookAccountSettingsStore
{
    /// <summary>Returns whether an account document exists.</summary>
    bool Exists();

    /// <summary>Loads the protected account document, if present.</summary>
    Task<OutlookAccountSettings?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the protected account document atomically.</summary>
    Task SaveAsync(OutlookAccountSettings settings, CancellationToken cancellationToken = default);
}
