using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

public interface IConfigurationDialogService
{
    PamCredentialUpdate? EditCredentials(bool hasExistingCredentials);
    RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday);
    MailConfigurationEdit? EditMail(MailSettings? current);
}
