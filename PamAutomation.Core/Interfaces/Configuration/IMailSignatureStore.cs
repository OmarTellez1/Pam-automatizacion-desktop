using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Interfaces.Configuration;

/// <summary>Persists the principal user's signature separately from collection content.</summary>
public interface IMailSignatureStore
{
    Task<MailSignature?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(MailSignature signature, CancellationToken cancellationToken = default);
}
