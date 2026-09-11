using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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

        try
        {
            credentialsWindow.Show();
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.ApplicationIdle);

            Assert.AreEqual("Configurar credenciales", credentialsWindow.Title);
            Assert.AreEqual(state, credentialsWindow.CurrentState);
            Assert.IsNotNull(credentialsWindow.FindName("PamOperationSpinner"));
            Assert.IsNotNull(credentialsWindow.FindName("OutlookOperationSpinner"));
            Assert.AreEqual("Configurar solicitud", requestWindow.Title);
            Assert.AreEqual("Configurar envío de correo", mailWindow.Title);
        }
        finally
        {
            credentialsWindow.Close();
            requestWindow.Close();
            mailWindow.Close();
        }
    }

    [STATestMethod]
    public void CredentialsWindow_ProgressAnimation_RunsOnlyDuringOperation()
    {
        EnsureApplicationResources();
        var workflow = new PendingCredentialWorkflow();
        var window = new CredentialsWindow(
            new CredentialConfigurationState(true, false, string.Empty),
            workflow,
            CancellationToken.None);

        try
        {
            window.Show();
            var spinner = (FrameworkElement)window.FindName("PamOperationSpinner");
            var saveButton = (Button)window.FindName("SavePamButton");

            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(Visibility.Collapsed, spinner.Visibility);
            Assert.IsFalse(spinner.RenderTransform.HasAnimatedProperties);

            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpDispatcherUntil(() => spinner.Visibility == Visibility.Visible);

            Assert.AreEqual(Visibility.Visible, spinner.Visibility);
            RotateTransform rotation = Assert.IsInstanceOfType<RotateTransform>(spinner.RenderTransform);
            Assert.IsTrue(rotation.HasAnimatedProperties);

            workflow.CompletePamOperation();
            PumpDispatcherUntil(() => spinner.Visibility == Visibility.Collapsed && saveButton.IsEnabled);

            Assert.AreEqual(Visibility.Collapsed, spinner.Visibility);
            Assert.IsFalse(spinner.RenderTransform.HasAnimatedProperties);
            Assert.IsTrue(saveButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void CredentialsWindow_UnexpectedWorkflowFailure_RestoresInterfaceAndShowsSafeError()
    {
        EnsureApplicationResources();
        var window = new CredentialsWindow(
            new CredentialConfigurationState(true, false, string.Empty),
            new ThrowingCredentialWorkflow(),
            CancellationToken.None);

        try
        {
            window.Show();
            var spinner = (FrameworkElement)window.FindName("PamOperationSpinner");
            var saveButton = (Button)window.FindName("SavePamButton");
            var operationText = (TextBlock)window.FindName("PamOperationText");

            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpDispatcherUntil(() =>
                spinner.Visibility == Visibility.Collapsed &&
                saveButton.IsEnabled &&
                operationText.Text.Contains("problema inesperado", StringComparison.Ordinal));

            Assert.Contains("problema inesperado", operationText.Text);
            Assert.AreEqual(Visibility.Collapsed, spinner.Visibility);
            Assert.IsTrue(saveButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    private static void PumpDispatcherUntil(Func<bool> condition)
    {
        DateTime timeout = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < timeout)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }

        Assert.IsTrue(condition(), "La interfaz no alcanzó el estado esperado antes del tiempo límite.");
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

    private sealed class PendingCredentialWorkflow : ICredentialConfigurationWorkflow
    {
        private readonly TaskCompletionSource<ConfigurationOperationResult> pamOperation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ConfigurationOperationResult> SaveAndTestPamAsync(
            PamCredentialUpdate update,
            CancellationToken cancellationToken = default) => pamOperation.Task;

        public Task<ConfigurationOperationResult> SaveAndVerifyOutlookAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfigurationOperationResult.Success("ok"));

        public Task<ConfigurationOperationResult> SendOutlookTestAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfigurationOperationResult.Success("ok"));

        public void CompletePamOperation() =>
            pamOperation.SetResult(ConfigurationOperationResult.Success("ok"));
    }

    private sealed class ThrowingCredentialWorkflow : ICredentialConfigurationWorkflow
    {
        public Task<ConfigurationOperationResult> SaveAndTestPamAsync(
            PamCredentialUpdate update,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ConfigurationOperationResult>(new InvalidOperationException("Simulated failure"));

        public Task<ConfigurationOperationResult> SaveAndVerifyOutlookAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ConfigurationOperationResult>(new InvalidOperationException("Simulated failure"));

        public Task<ConfigurationOperationResult> SendOutlookTestAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ConfigurationOperationResult>(new InvalidOperationException("Simulated failure"));
    }
}
