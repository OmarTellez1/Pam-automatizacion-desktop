using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Services;

public interface IUserDialogService
{
    bool ConfirmRealExecution(PamExecutionPreparation preparation, MailSettings? mailSettings);
    bool ConfirmTestEmail(string recipient);
    bool ConfirmOutlookTestEmail(string recipient);
    void ShowResult(string title, string message);
    void ShowError(string message);
}
