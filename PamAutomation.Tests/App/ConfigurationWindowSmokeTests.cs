using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App;
using PamAutomation.App.Services;
using PamAutomation.App.Views;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Execution;

namespace PamAutomation.Tests.App;

[TestClass]
[DoNotParallelize]
public sealed class ConfigurationWindowSmokeTests
{
    [STATestMethod]
    public void MainWindow_WithDiagnosticAction_RendersSuccessfully()
    {
        EnsureApplicationResources();
        var viewModel = new MainWindowViewModel
        {
            IsUpdateAvailable = true,
            UpdateNotificationMessage = "Hay una actualización disponible."
        };
        var window = new MainWindow { DataContext = viewModel };

        try
        {
            window.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.IsNotNull(window.FindName("ExportDiagnosticsButton"));
            Assert.IsNotNull(window.FindName("AccessPamButton"));
            Assert.IsNotNull(window.FindName("PamAccountsButton"));
            Assert.IsNotNull(window.FindName("AboutButton"));
            Assert.IsNotNull(window.FindName("CollectionsButton"));
            Assert.IsNull(window.FindName("UseCollectionsToggle"));
            var brandBadge = Assert.IsInstanceOfType<Border>(window.FindName("BrandMonogramBadge"));
            var brandMonogram = Assert.IsInstanceOfType<TextBlock>(window.FindName("BrandMonogram"));
            var updateBanner = Assert.IsInstanceOfType<Border>(window.FindName("UpdateNotificationBanner"));
            var startWarning = Assert.IsInstanceOfType<Border>(window.FindName("StartAutomationWarningBanner"));
            var automationStatus = Assert.IsInstanceOfType<TextBlock>(window.FindName("AutomationStatusText"));
            var resultButton = Assert.IsInstanceOfType<Button>(window.FindName("ShowAutomationResultButton"));
            Assert.AreEqual(Visibility.Visible, updateBanner.Visibility);
            Assert.AreEqual(Visibility.Visible, startWarning.Visibility);
            Assert.AreEqual(Visibility.Collapsed, automationStatus.Visibility);
            Assert.AreEqual(Visibility.Collapsed, resultButton.Visibility);
            Assert.AreEqual("PAM Automation", window.Title);
            Assert.AreEqual(WindowState.Maximized, window.WindowState);
            Assert.AreEqual(TimeSpan.FromMinutes(1), MainWindow.RequestTimeRefreshInterval);
            Assert.AreEqual(48d, brandBadge.Width);
            Assert.AreEqual(48d, brandBadge.Height);
            Assert.AreEqual("PA", brandMonogram.Text);

            viewModel.IsRunning = true;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.AreEqual(Visibility.Visible, automationStatus.Visibility);
        }
        finally
        {
            window.Close();
        }
    }

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
            Assert.IsNull(requestWindow.FindName("ReasonBox"));
            var hourBox = Assert.IsInstanceOfType<ComboBox>(requestWindow.FindName("HourBox"));
            var minuteBox = Assert.IsInstanceOfType<TextBox>(requestWindow.FindName("MinuteBox"));
            Assert.HasCount(12, hourBox.Items);
            Assert.AreEqual("01", hourBox.Items[0]);
            Assert.AreEqual("12", hourBox.Items[11]);
            Assert.AreEqual("05", minuteBox.Text);
            Assert.AreEqual(2, minuteBox.MaxLength);
            Assert.IsNull(requestWindow.FindName("TestModeBox"));
            Assert.AreEqual("Configurar envío de correo", mailWindow.Title);
            Assert.AreEqual(700d, mailWindow.Width);
            Assert.AreEqual(620d, mailWindow.Height);
            Assert.IsNotNull(mailWindow.FindName("BodyEditor"));
        }
        finally
        {
            credentialsWindow.Close();
            requestWindow.Close();
            mailWindow.Close();
        }
    }

    [STATestMethod]
    public void PamAccountWindows_WithApplicationResources_LoadSuccessfully()
    {
        EnsureApplicationResources();
        var principal = new PamAccountSummary(PamAccountId.New(), "Principal", true);
        var accountsWindow = new PamAccountsWindow(new StaticPamAccountService(principal));
        var selectionWindow = new PamAccountSelectionWindow(
            [new PamAccountStatus(principal, true)]);
        var setupWindow = new PamAccountNameWindow();
        var secondaryWindow = new PamSecondaryAccountWindow();
        var aboutWindow = new AboutWindow();

        try
        {
            accountsWindow.Show();
            selectionWindow.Show();
            setupWindow.Show();
            secondaryWindow.Show();
            aboutWindow.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var accountsList = Assert.IsInstanceOfType<ListBox>(accountsWindow.FindName("AccountsList"));
            Assert.AreEqual(HorizontalAlignment.Stretch, accountsList.HorizontalContentAlignment);
            Assert.IsNotNull(accountsWindow.FindName("CreateSecondaryButton"));
            Assert.IsNotNull(selectionWindow.FindName("AccountsList"));
            Assert.IsNotNull(selectionWindow.FindName("ContinueButton"));
            Assert.IsNotNull(setupWindow.FindName("NameBox"));
            Assert.IsNotNull(secondaryWindow.FindName("UsernameBox"));
            Assert.IsNotNull(secondaryWindow.FindName("PasswordBox"));
            Assert.IsNotNull(secondaryWindow.FindName("TotpSeedBox"));
            Assert.AreEqual("1.0.2", aboutWindow.VersionText);
            var author = Assert.IsInstanceOfType<TextBlock>(aboutWindow.FindName("AuthorText"));
            Assert.AreEqual("Hitss Omar Téllez", author.Text);
            Assert.IsNotNull(aboutWindow.FindName("CloseButton"));
        }
        finally
        {
            aboutWindow.Close();
            secondaryWindow.Close();
            setupWindow.Close();
            selectionWindow.Close();
            accountsWindow.Close();
        }
    }

    [STATestMethod]
    public void RepeatedPendingDecisionWindow_CompleteEmailSet_EnablesAllThreeActions()
    {
        EnsureApplicationResources();
        var request = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 21),
            StartTime = new TimeOnly(17, 0),
            SessionHours = 1,
            RequestReason = "Solicitud de acceso PAM",
        };
        var preparation = new PamExecutionPreparation(
            2,
            request,
            FinalAction.SendEmail,
            pendingRequestIdsBeforeSubmission: ["154210", "154211"],
            exactPendingDuplicateCount: 2,
            reusablePendingRequestIds: ["154210", "154211"]);
        var window = new RepeatedPendingDecisionWindow(preparation);

        try
        {
            window.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.IsNotNull(window.FindName("CancelButton"));
            Assert.IsNotNull(window.FindName("SubmitAgainButton"));
            var reuse = Assert.IsInstanceOfType<Button>(window.FindName("ReuseButton"));
            Assert.IsTrue(reuse.IsEnabled);
            Assert.AreEqual(RepeatedPendingDecision.Cancel, window.Decision);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void FavoritesCatalogWindow_FirstUse_RendersEmptyStateAndObtainAction()
    {
        EnsureApplicationResources();
        var viewModel = new FavoritesCatalogViewModel(new EmptyFavoriteCatalogService());
        var window = new FavoritesCatalogWindow(viewModel);

        try
        {
            window.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var emptyState = Assert.IsInstanceOfType<Border>(window.FindName("EmptyFavoritesState"));
            var list = Assert.IsInstanceOfType<ListBox>(window.FindName("FavoritesList"));
            var refresh = Assert.IsInstanceOfType<Button>(window.FindName("RefreshFavoritesButton"));
            Assert.IsNotNull(window.FindName("CreateCollectionButton"));
            Assert.IsNotNull(window.FindName("CollectionsList"));
            Assert.AreEqual(Visibility.Visible, emptyState.Visibility);
            Assert.AreEqual(Visibility.Collapsed, list.Visibility);
            Assert.AreEqual("Obtener solicitudes favoritas", refresh.Content);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void FavoriteCollectionEditor_RendersSelectionAndReasonFields()
    {
        EnsureApplicationResources();
        var favorites = new[]
        {
            new FavoriteCatalogEntry("system", "NOSQLBOOSTER", "account", "10.31.32.25")
        };
        var window = new FavoriteCollectionEditorWindow(
            favorites,
            null);

        try
        {
            window.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.IsNotNull(window.FindName("CollectionNameBox"));
            Assert.IsNotNull(window.FindName("FavoriteSelectionList"));
            Assert.IsNull(window.FindName("StartDatePicker"));
            Assert.IsNotNull(window.FindName("ReasonBox"));
            Assert.IsNotNull(window.FindName("SaveCollectionButton"));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void FavoriteCollectionEditor_AllFavorites_LocksNameAndSelection()
    {
        EnsureApplicationResources();
        var favorites = new[]
        {
            new FavoriteCatalogEntry("system", "NOSQLBOOSTER", "account", "10.31.32.25")
        };
        var allFavorites = new FavoriteCollection(
            Guid.NewGuid(),
            FavoriteCollection.AllFavoritesName,
            "Soporte",
            [FavoriteAccountKey.From(favorites[0])],
            isAllFavorites: true);
        var window = new FavoriteCollectionEditorWindow(favorites, allFavorites);

        try
        {
            window.Show();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var name = Assert.IsInstanceOfType<TextBox>(window.FindName("CollectionNameBox"));
            var selection = Assert.IsInstanceOfType<ListBox>(window.FindName("FavoriteSelectionList"));
            var notice = Assert.IsInstanceOfType<Border>(window.FindName("AllFavoritesNotice"));
            Assert.IsTrue(name.IsReadOnly);
            Assert.IsFalse(selection.IsEnabled);
            Assert.AreEqual(Visibility.Visible, notice.Visibility);
        }
        finally
        {
            window.Close();
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
        string applicationAssemblyName = Uri.EscapeDataString(
            typeof(MainWindow).Assembly.GetName().Name ?? throw new InvalidOperationException());

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/{applicationAssemblyName};component/Resources/Colors.xaml",
                UriKind.Absolute)
        });
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/{applicationAssemblyName};component/Resources/Styles.xaml",
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

    private sealed class EmptyFavoriteCatalogService : IFavoriteCatalogService
    {
        public Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<FavoriteCatalogSnapshot?>(null);

        public Task<FavoriteCatalogRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StaticPamAccountService(PamAccountSummary principal) : IPamAccountService
    {
        public Task<IReadOnlyList<PamAccountStatus>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PamAccountStatus>>([new PamAccountStatus(principal, true)]);
        public Task<PamCredentials?> LoadCredentialsAsync(PamAccountId accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PamCredentials?>(new PamCredentials("user", "password", "JBSWY3DPEHPK3PXP"));
        public Task<PamAccountSummary> CreatePrincipalAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> RenameAsync(PamAccountId accountId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> CreateSecondaryAsync(string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> UpdateSecondaryAsync(PamAccountId accountId, string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteSecondaryAsync(PamAccountId accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
