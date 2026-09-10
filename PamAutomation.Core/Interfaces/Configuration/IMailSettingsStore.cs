using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Interfaces.Configuration;

public interface IMailSettingsStore
{
    bool Exists();
    Task<MailSettings?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(MailSettings settings, CancellationToken cancellationToken = default);
}
