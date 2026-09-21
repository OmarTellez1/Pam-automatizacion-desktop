using System.Net.Http;
using System.Diagnostics;
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
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Infrastructure.Accounts;
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
        private PamAccountService? pamAccountService;
        private bool handlingFatalDispatcherException;

        protected override async void OnStartup(StartupEventArgs e)
        {
            var startupTimer = Stopwatch.StartNew();
            base.OnStartup(e);

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(ApplyWindowTitleBarAppearance));

            runtimeLoggerFactory = RuntimeDiagnosticLoggerFactory.CreateForCurrentProcess();
            loggerFactory = runtimeLoggerFactory;
            var pendingDiagnosticStore = new InMemoryPendingReconciliationDiagnosticStore();
            diagnosticExportService = new DiagnosticExportService(
                runtimeLoggerFactory,
                pendingDiagnosticSource: pendingDiagnosticStore);
            userDialogs = new WpfUserDialogService();
            logger = loggerFactory.CreateLogger<App>();
            DispatcherUnhandledException += LogDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
            TaskScheduler.UnobservedTaskException += LogUnobservedTaskException;
            logger.LogInformation(
                "Pam Automation application startup began at {Stage}. Elapsed={ElapsedMilliseconds}ms.",
                "ManagedStartup",
                startupTimer.ElapsedMilliseconds);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            pamAccountService = new PamAccountService(
                new DpapiPamAccountRegistryStore(),
                loggerFactory.CreateLogger<PamAccountService>());
            if (!await EnsurePrincipalIdentityAsync(pamAccountService, applicationLifetime.Token)) return;

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
            var favoriteCatalog = new FavoriteCatalogService(
                favorites,
                credentialStore,
                new DpapiFavoriteCatalogStore(),
                TimeProvider.System);
            var favoriteCollections = new FavoriteCollectionService(
                new DpapiFavoriteCollectionStore());
            var execution = new PamAutomationExecutionService(
                favorites,
                new PamRequestSubmissionService(
                    authentication,
                    favorites,
                    new PamRequestSubmissionPortalFactory(),
                    requestValidator),
                new PamPendingService(
                    authentication,
                    new PamPendingPortalFactory(),
                    delay,
                    pendingDiagnosticStore),
                new PamRequestTestService(
                    authentication,
                    favorites,
                    new PamRequestTestPortalFactory(),
                    requestValidator),
                settingsValidator,
                TimeProvider.System,
                outlookClient,
                new MailSettingsValidator(),
                loggerFactory.CreateLogger<PamAutomationExecutionService>(),
                pendingDiagnosticSink: pendingDiagnosticStore);
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
                    loggerFactory.CreateLogger<MailSettingsWindow>(),
                    favoriteCatalog,
                    favoriteCollections,
                    loggerFactory.CreateLogger<FavoritesCatalogViewModel>(),
                    pamAccountService,
                    loggerFactory.CreateLogger<PamAccountsWindow>()),
                mailSettingsStore,
                new MailSettingsValidator(),
                outlookAccountStore,
                loggerFactory.CreateLogger<MainWindowViewModel>(),
                diagnosticExportService,
                updateService,
                typeof(App).Assembly.GetName().Version,
                interactiveAccess,
                favoriteCollections,
                pamAccountService);
            viewModel.BeginInitialization();
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            logger.LogInformation(
                "Pam Automation startup reached {Stage}. Elapsed={ElapsedMilliseconds}ms.",
                "MainWindowShown",
                startupTimer.ElapsedMilliseconds);
            await viewModel.InitializeAsync(applicationLifetime.Token);
            logger.LogInformation(
                "Pam Automation startup reached {Stage}. Elapsed={ElapsedMilliseconds}ms.",
                "ConfigurationReady",
                startupTimer.ElapsedMilliseconds);
            logger.LogInformation(
                "Pam Automation application startup completed at {Stage}. Elapsed={ElapsedMilliseconds}ms.",
                "ReadyForUser",
                startupTimer.ElapsedMilliseconds);

            _ = CheckForUpdateAfterStartupAsync(viewModel, applicationLifetime.Token);
        }

        private async Task<bool> EnsurePrincipalIdentityAsync(
            IPamAccountService accountService,
            CancellationToken cancellationToken)
        {
            try
            {
                var bootstrapper = new PrincipalIdentityBootstrapper(accountService);
                PrincipalIdentityBootstrapResult result = await bootstrapper.EnsureAsync(() =>
                {
                    var setup = new PamAccountNameWindow();
                    return setup.ShowDialog() == true ? setup.AccountName : null;
                }, cancellationToken);
                if (result == PrincipalIdentityBootstrapResult.Cancelled)
                {
                    logger.LogInformation("First-run principal identity setup was cancelled.");
                    Shutdown();
                    return false;
                }

                logger.LogInformation(
                    "Principal PAM identity bootstrap completed. Status={Status}.",
                    result);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Shutdown();
                return false;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Principal PAM identity initialization failed.");
                UrgentMessageBox.Show(
                    exception is PamAccountException ? exception.Message :
                        "No fue posible preparar el usuario principal. Exporta un diagnóstico si el problema continúa.",
                    "No se pudo iniciar PAM Automation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return false;
            }
        }

        private async Task CheckForUpdateAfterStartupAsync(
            MainWindowViewModel viewModel,
            CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            await viewModel.CheckForUpdateAsync(cancellationToken);
            logger.LogInformation(
                "Post-startup task reached {Stage}. Elapsed={ElapsedMilliseconds}ms.",
                "UpdateCheckCompleted",
                timer.ElapsedMilliseconds);
        }

        private static void ApplyWindowTitleBarAppearance(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                WindowTitleBarAppearance.TryApply(window);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            logger.LogInformation("Pam Automation application shutdown.");
            applicationLifetime.Cancel();
            DispatcherUnhandledException -= LogDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= LogUnhandledException;
            TaskScheduler.UnobservedTaskException -= LogUnobservedTaskException;
            updateHttpClient?.Dispose();
            pamAccountService?.Dispose();
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
