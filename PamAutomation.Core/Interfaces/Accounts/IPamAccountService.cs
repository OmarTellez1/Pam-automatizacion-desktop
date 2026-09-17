using PamAutomation.Core.Accounts;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Interfaces.Accounts;

/// <summary>Administers the principal identity and secondary interactive PAM accounts.</summary>
public interface IPamAccountService
{
    Task<IReadOnlyList<PamAccountStatus>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<PamCredentials?> LoadCredentialsAsync(PamAccountId accountId, CancellationToken cancellationToken = default);
    Task<PamAccountSummary> CreatePrincipalAsync(string name, CancellationToken cancellationToken = default);
    Task<PamAccountSummary> RenameAsync(PamAccountId accountId, string name, CancellationToken cancellationToken = default);
    Task<PamAccountSummary> CreateSecondaryAsync(string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default);
    Task<PamAccountSummary> UpdateSecondaryAsync(PamAccountId accountId, string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default);
    Task DeleteSecondaryAsync(PamAccountId accountId, CancellationToken cancellationToken = default);
}
