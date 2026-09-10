using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.App.Views;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Tests.App;

[TestClass]
[DoNotParallelize]
public sealed class ConfigurationWindowSmokeTests
{
    [STATestMethod]
    public void ConfigurationWindows_WithApplicationResources_LoadSuccessfully()
    {
        EnsureApplicationResources();
        var state = new CredentialConfigurationState(true, true, "sender@example.test");
        var request = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 10),
            StartTime = new TimeOnly(17, 5),
            SessionHours = 1,
            RequestReason = "Prueba visual"
        };
        var mail = new MailSettings
        {
            SmtpAddress = "sender@example.test",
            ToRecipients = ["recipient@example.test"],
            Subject = "Accesos",
            HtmlTemplate = "<p>{{REQUEST_IDS}}</p>"
        };

        var credentialsWindow = new CredentialsWindow(
            state,
            new FakeCredentialWorkflow(),
            CancellationToken.None);
        var requestWindow = new RequestSettingsWindow(request, testModeEnabled: true, request.StartDate);
        var mailWindow = new MailSettingsWindow(mail, mail.SmtpAddress);

        Assert.AreEqual("Configurar credenciales", credentialsWindow.Title);
        Assert.AreEqual(state, credentialsWindow.CurrentState);
        Assert.AreEqual("Configurar solicitud", requestWindow.Title);
        Assert.AreEqual("Configurar envío de correo", mailWindow.Title);
        credentialsWindow.Close();
        requestWindow.Close();
        mailWindow.Close();
    }

    private static void EnsureApplicationResources()
    {
        Application application = Application.Current ?? new Application();
        if (application.Resources.MergedDictionaries.Count > 0) return;

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/PamAutomation.App;component/Resources/Colors.xaml",
                UriKind.Absolute)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/PamAutomation.App;component/Resources/Styles.xaml",
                UriKind.Absolute)
        });
    }

    private sealed class FakeCredentialWorkflow : ICredentialConfigurationWorkflow
    {
        public Task<ConfigurationOperationResult> SaveAndTestPamAsync(
            PamCredentialUpdate update,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfigurationOperationResult.Success("ok"));

        public Task<ConfigurationOperationResult> SaveAndVerifyOutlookAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfigurationOperationResult.Success("ok"));

        public Task<ConfigurationOperationResult> SendOutlookTestAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfigurationOperationResult.Success("ok"));
    }
}
