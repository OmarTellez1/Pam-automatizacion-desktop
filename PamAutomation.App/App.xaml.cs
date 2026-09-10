using System.Windows;
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
using PamAutomation.Infrastructure.Time;
using PamAutomation.Outlook;

namespace PamAutomation.App
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var delay = new TaskAsyncDelay();
            var requestValidator = new RequestSettingsValidator();
            var settingsValidator = new AppSettingsValidator(requestValidator);
            var authentication = new PamAuthenticationService(
                new PamBrowserSessionFactory(),
                new PamAuthenticationPortalFactory(),
                new PamHttpDateClockFactory(TimeProvider.System),
                new TotpGenerator(),
                delay);
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
                new OutlookClassicClient(),
                new MailSettingsValidator());
            var viewModel = new MainWindowViewModel(
                execution,
                new JsonAppSettingsStore(),
                new WindowsPamCredentialStore(),
                new WpfUserDialogService(),
                settingsValidator,
                TimeProvider.System,
                new WpfConfigurationDialogService(),
                new DpapiMailSettingsStore(),
                new OutlookClassicClient(),
                new MailSettingsValidator());
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
            await viewModel.InitializeAsync();
        }
    }
}
