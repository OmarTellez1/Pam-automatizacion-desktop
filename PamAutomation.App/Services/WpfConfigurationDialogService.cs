using System.Windows;
using PamAutomation.App.Views;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

public sealed class WpfConfigurationDialogService : IConfigurationDialogService
{
    public PamCredentialUpdate? EditCredentials(bool hasExistingCredentials)
    {
        var dialog = new CredentialsWindow(hasExistingCredentials) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Update : null;
    }

    public RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday)
    {
        var dialog = new RequestSettingsWindow(current, testModeEnabled, ecuadorToday) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Edit : null;
    }

    public MailConfigurationEdit? EditMail(MailSettings? current)
    {
        var dialog = new MailSettingsWindow(current) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true && dialog.Settings is not null
            ? new MailConfigurationEdit(dialog.Settings, dialog.SendTestRequested)
            : null;
    }
}
