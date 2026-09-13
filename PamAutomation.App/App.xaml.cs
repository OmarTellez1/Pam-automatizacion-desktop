using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Services;
using PamAutomation.App.ViewModels;
using PamAutomation.App.Views;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Totp;
using PamAutomation.Core.Validation;
using PamAutomation.Infrastructure.Configuration;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Diagnostics;
using PamAutomation.Infrastructure.Time;
using PamAutomation.Infrastructure.Updates;
using PamAutomation.Outlook;

namespace PamAutomation.App
{
    public partial class App : Application
    {
        private static readonly Uri UpdateManifestUri = new(
            "https://raw.githubusercontent.com/OmarTellez1/Pam-automation-installer/main/latest.json");
        private static readonly Uri UpdateRepositoryUri = new(
            "https://github.com/OmarTellez1/Pam-automation-installer");

        private RuntimeDiagnosticLoggerFactory? runtimeLoggerFactory;
        private ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
        private ILogger<App> logger = NullLogger<App>.Instance;
        private IDiagnosticExportService? diagnosticExportService;
        private WpfUserDialogService? userDialogs;
        private readonly CancellationTokenSource applicationLifetime = new();
        private HttpClient? updateHttpClient;
        private bool handlingFatalDispatcherException;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            runtimeLoggerFactory = RuntimeDiagnosticLoggerFactory.CreateForCurrentProcess();
            loggerFactory = runtimeLoggerFactory;
            diagnosticExportService = new DiagnosticExportService(runtimeLoggerFactory);
            userDialogs = new WpfUserDialogService();
            logger = loggerFactory.CreateLogger<App>();
            DispatcherUnhandledException += LogDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
            TaskScheduler.UnobservedTaskException += LogUnobservedTaskException;
            logger.LogInformation("Pam Automation application startup began.");

            var delay = new TaskAsyncDelay();
            var requestValidator = new RequestSettingsValidator();
            var settingsValidator = new AppSettingsValidator(requestValidator);
            var authentication = new PamAuthenticationService(
                new PamBrowserSessionFactory(),
                new PamAuthenticationPortalFactory(),
                new PamHttpDateClockFactory(TimeProvider.System),
                new TotpGenerator(),
                delay);
            var interactiveAuthentication = new PamAuthenticationService(
                new PamInteractiveBrowserSessionFactory(
                    new WpfPamCertificateTrustPrompt(),
                    loggerFactory.CreateLogger<PamInteractiveBrowserSessionFactory>()),
                new PamAuthenticationPortalFactory(),
                new PamHttpDateClockFactory(TimeProvider.System),
                new TotpGenerator(),
                delay);
            var interactiveAccess = new PamInteractiveAccessService(
                interactiveAuthentication,
                loggerFactory.CreateLogger<PamInteractiveAccessService>());
            var credentialStore = new WindowsPamCredentialStore();
            var outlookClient = new OutlookClassicClient(
                loggerFactory.CreateLogger<OutlookClassicClient>());
            var outlookAccountStore = new DpapiOutlookAccountSettingsStore();
            var mailSettingsStore = new DpapiMailSettingsStore();
            updateHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            var updateService = new PublicManifestApplicationUpdateService(
                updateHttpClient,
                UpdateManifestUri,
                UpdateRepositoryUri,
                loggerFactory.CreateLogger<PublicManifestApplicationUpdateService>());
            var credentialWorkflow = new CredentialConfigurationWorkflow(
                credentialStore,
                authentication,
                outlookAccountStore,
                outlookClient,
                loggerFactory.CreateLogger<CredentialConfigurationWorkflow>());
            var favorites = new PamFavoritesService(authentication, new PamFavoritesPortalFactory());
            var execution = new PamAutomationExecutionService(
                favorites,
                new PamRequestSubmissionService(
                    authentication,
                    favorites,
                    new PamRequestSubmissionPortalFactory(),
                    requestValidator),
                new PamPendingService(authentication, new PamPendingPortalFactory(), delay),
                new PamRequestTestService(
                    authentication,
                    favorites,
                    new PamRequestTestPortalFactory(),
                    requestValidator),
                settingsValidator,
                TimeProvider.System,
                outlookClient,
                new MailSettingsValidator(),
                loggerFactory.CreateLogger<PamAutomationExecutionService>());
            var viewModel = new MainWindowViewModel(
                execution,
                new JsonAppSettingsStore(),
                credentialStore,
                userDialogs,
                settingsValidator,
                TimeProvider.System,
                new WpfConfigurationDialogService(
                    credentialWorkflow,
                    loggerFactory.CreateLogger<CredentialsWindow>(),
                    loggerFactory.CreateLogger<MailSettingsWindow>()),
                mailSettingsStore,
                new MailSettingsValidator(),
                outlookAccountStore,
                loggerFactory.CreateLogger<MainWindowViewModel>(),
                diagnosticExportService,
                updateService,
                typeof(App).Assembly.GetName().Version,
                interactiveAccess);
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
            await viewModel.InitializeAsync(applicationLifetime.Token);
            await viewModel.CheckForUpdateAsync(applicationLifetime.Token);
            logger.LogInformation("Pam Automation application startup completed.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            logger.LogInformation("Pam Automation application shutdown.");
            applicationLifetime.Cancel();
            DispatcherUnhandledException -= LogDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= LogUnhandledException;
            TaskScheduler.UnobservedTaskException -= LogUnobservedTaskException;
            updateHttpClient?.Dispose();
            applicationLifetime.Dispose();
            loggerFactory.Dispose();
            base.OnExit(e);
        }

        private async void LogDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            logger.LogCritical(e.Exception, "Unhandled WPF dispatcher exception. The application will close safely.");
            e.Handled = true;
            if (handlingFatalDispatcherException)
            {
                Shutdown(1);
                return;
            }

            handlingFatalDispatcherException = true;
            try
            {
                MessageBoxResult exportChoice = UrgentMessageBox.Show(
                    "Ocurrió un problema inesperado en la interfaz. La aplicación se cerrará de forma segura. " +
                    "¿Deseas exportar un diagnóstico seguro antes de cerrar?",
                    "Pam Automation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error,
                    MessageBoxResult.Yes);
                if (exportChoice == MessageBoxResult.Yes && diagnosticExportService is not null && userDialogs is not null)
                {
                    string suggestedName = $"pam-automation-diagnostic-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip";
                    string? destinationPath = userDialogs.SelectDiagnosticExportPath(suggestedName);
                    if (!string.IsNullOrWhiteSpace(destinationPath))
                    {
                        DiagnosticExportContext context = (MainWindow?.DataContext as MainWindowViewModel)?.CaptureDiagnosticContext()
                            ?? new DiagnosticExportContext(false, false, false, null);
                        await diagnosticExportService.ExportAsync(destinationPath, context).ConfigureAwait(true);
                        UrgentMessageBox.Show(
                            "El diagnóstico se exportó correctamente. Puedes enviarlo a soporte.",
                            "Diagnóstico exportado",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception notificationException)
            {
                logger.LogError(notificationException, "The fatal WPF diagnostic export or notification failed.");
            }
            finally
            {
                Shutdown(1);
            }
        }

        private void LogUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                logger.LogCritical(exception, "Unhandled application-domain exception. Terminating={IsTerminating}.", e.IsTerminating);
            }
        }

        private void LogUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.LogError(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        }
    }
}
