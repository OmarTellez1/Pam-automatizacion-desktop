using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Accounts;

namespace PamAutomation.App.Services;

public interface IConfigurationDialogService
{
    Task<CredentialConfigurationState> EditCredentialsAsync(
        CredentialConfigurationState current,
        CancellationToken cancellationToken = default);
    RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday);
    MailSignature? EditSignature(MailSignature? current);
    void OpenFavoriteCollections(string smtpAddress);
    void OpenPamAccounts();
    void OpenAbout();
    PamAccountId? SelectPamAccount(IReadOnlyList<PamAccountStatus> accounts);
}
