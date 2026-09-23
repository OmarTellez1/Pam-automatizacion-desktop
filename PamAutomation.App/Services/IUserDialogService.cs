using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Services;

public interface IUserDialogService
{
    RepeatedPendingDecision ChooseRepeatedPendingAction(PamExecutionPreparation preparation);
    bool ConfirmAutomationStart(AppSettings settings, MailSettings? mailSettings, string? collectionName = null);
    bool ConfirmTestEmail(string recipient);
    bool ConfirmOutlookTestEmail(string recipient);
    void ShowResult(string title, string message, string? requestIdsText = null);
    void ShowError(string message);
    string? SelectDiagnosticExportPath(string suggestedFileName);
}
