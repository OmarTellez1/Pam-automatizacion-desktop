using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Services;
using PamAutomation.App.ViewModels;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Totp;
using PamAutomation.Core.Validation;
using PamAutomation.Infrastructure.Configuration;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Diagnostics;
using PamAutomation.Infrastructure.Time;
using PamAutomation.Outlook;

namespace PamAutomation.App
{
    public partial class App : Application
    {
        private ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
        private ILogger<App> logger = NullLogger<App>.Instance;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            loggerFactory = DevelopmentFileLoggerFactory.CreateForCurrentProcess();
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
            var credentialStore = new WindowsPamCredentialStore();
            var outlookClient = new OutlookClassicClient(
                loggerFactory.CreateLogger<OutlookClassicClient>());
            var outlookAccountStore = new DpapiOutlookAccountSettingsStore();
            var mailSettingsStore = new DpapiMailSettingsStore();
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
                new WpfUserDialogService(),
                settingsValidator,
                TimeProvider.System,
                new WpfConfigurationDialogService(credentialWorkflow),
                mailSettingsStore,
                new MailSettingsValidator(),
                outlookAccountStore,
                loggerFactory.CreateLogger<MainWindowViewModel>());
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
            await viewModel.InitializeAsync();
            logger.LogInformation("Pam Automation application startup completed.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            logger.LogInformation("Pam Automation application shutdown.");
            DispatcherUnhandledException -= LogDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= LogUnhandledException;
            TaskScheduler.UnobservedTaskException -= LogUnobservedTaskException;
            loggerFactory.Dispose();
            base.OnExit(e);
        }

        private void LogDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) =>
            logger.LogCritical(e.Exception, "Unhandled WPF dispatcher exception.");

        private void LogUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                logger.LogCritical(exception, "Unhandled application-domain exception. Terminating={IsTerminating}.", e.IsTerminating);
            }
        }

        private void LogUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) =>
            logger.LogError(e.Exception, "Unobserved task exception.");
    }
}
