using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Views;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

public sealed class WpfConfigurationDialogService : IConfigurationDialogService
{
    private readonly ICredentialConfigurationWorkflow credentialWorkflow;
    private readonly ILogger<CredentialsWindow> credentialsWindowLogger;

    public WpfConfigurationDialogService(
        ICredentialConfigurationWorkflow credentialWorkflow,
        ILogger<CredentialsWindow>? credentialsWindowLogger = null)
    {
        this.credentialWorkflow = credentialWorkflow ?? throw new ArgumentNullException(nameof(credentialWorkflow));
        this.credentialsWindowLogger = credentialsWindowLogger ?? NullLogger<CredentialsWindow>.Instance;
    }

    public Task<CredentialConfigurationState> EditCredentialsAsync(
        CredentialConfigurationState current,
        CancellationToken cancellationToken = default)
    {
        var dialog = new CredentialsWindow(current, credentialWorkflow, cancellationToken, credentialsWindowLogger)
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
