using System.Windows;
using PamAutomation.App.Views;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

public sealed class WpfConfigurationDialogService : IConfigurationDialogService
{
    private readonly ICredentialConfigurationWorkflow credentialWorkflow;

    public WpfConfigurationDialogService(ICredentialConfigurationWorkflow credentialWorkflow)
    {
        this.credentialWorkflow = credentialWorkflow ?? throw new ArgumentNullException(nameof(credentialWorkflow));
    }

    public Task<CredentialConfigurationState> EditCredentialsAsync(
        CredentialConfigurationState current,
        CancellationToken cancellationToken = default)
    {
        var dialog = new CredentialsWindow(current, credentialWorkflow, cancellationToken)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        return Task.FromResult(dialog.CurrentState);
    }

    public RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday)
    {
        var dialog = new RequestSettingsWindow(current, testModeEnabled, ecuadorToday) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Edit : null;
    }

    public MailConfigurationEdit? EditMail(MailSettings? current, string smtpAddress)
    {
        var dialog = new MailSettingsWindow(current, smtpAddress) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true && dialog.Settings is not null
            ? new MailConfigurationEdit(dialog.Settings)
            : null;
    }
}
