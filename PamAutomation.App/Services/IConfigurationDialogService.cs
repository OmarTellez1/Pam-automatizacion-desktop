using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

public interface IConfigurationDialogService
{
    Task<CredentialConfigurationState> EditCredentialsAsync(
        CredentialConfigurationState current,
        CancellationToken cancellationToken = default);
    RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday);
    MailConfigurationEdit? EditMail(MailSettings? current, string smtpAddress);
    void OpenFavoriteCollections();
    void OpenPamAccounts();
}
