using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Interfaces.Execution;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Interfaces.Updates;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Updates;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public void InitialState_IsSafeAndIncomplete()
    {
        var viewModel = new MainWindowViewModel();

        Assert.IsTrue(viewModel.AreControlsEnabled);
        Assert.IsFalse(viewModel.IsConfigurationComplete);
        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.IsFalse(viewModel.IsTestActionVisible);
        Assert.IsFalse(viewModel.SendEmailAtEnd);
        Assert.AreEqual("Configuración pendiente", viewModel.ConfigurationSummaryText);
        Assert.AreEqual("PAM Automation V. 0.0.1", viewModel.ApplicationDisplayName);
    }

    [TestMethod]
    public async Task InitializeAsync_RequestStartTimeInPast_ExplainsWhyAutomationIsDisabled()
    {
        var now = new DateTimeOffset(2026, 9, 11, 17, 30, 0, TimeSpan.FromHours(-5));
        var settings = new AppSettings
        {
            Request = new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 11),
                StartTime = new TimeOnly(17, 0),
                SessionHours = 1,
                RequestReason = "Prueba de aviso"
            }
        };
        var viewModel = new MainWindowViewModel(
            new FakeExecutionService(now),
            new FakeSettingsStore(settings),
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()));

        await viewModel.InitializeAsync();

        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.IsFalse(viewModel.CanAccessPam);
        Assert.IsTrue(viewModel.IsStartAutomationWarningVisible);
        Assert.Contains("hora de inicio configurada ya pasó", viewModel.StartAutomationWarningMessage);
        Assert.Contains("Configurar solicitud", viewModel.StartAutomationWarningMessage);
    }

    [TestMethod]
    public async Task InitializeAsync_CompleteConfiguration_HidesAutomationWarning()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);

        await context.ViewModel.InitializeAsync();

        Assert.IsTrue(context.ViewModel.CanStartAutomation);
        Assert.IsFalse(context.ViewModel.IsStartAutomationWarningVisible);
        Assert.AreEqual(string.Empty, context.ViewModel.StartAutomationWarningMessage);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_NewerVersion_ShowsDismissibleNotice()
    {
        var updateService = new FakeUpdateService(
            new ApplicationUpdateResult(new Version(0, 0, 2, 0), true));
        var viewModel = new MainWindowViewModel(
            null, null, null, null,
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            updateService: updateService,
            applicationVersion: new Version(0, 0, 1, 0));

        await viewModel.CheckForUpdateAsync();

        Assert.IsTrue(viewModel.IsUpdateAvailable);
        Assert.Contains("V. 0.0.2", viewModel.UpdateNotificationMessage);
        viewModel.DismissUpdateCommand.Execute(null);
        Assert.IsFalse(viewModel.IsUpdateAvailable);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_RemoteFailure_DoesNotInterruptApplication()
    {
        var updateService = new FakeUpdateService(new HttpRequestException("network detail"));
        var viewModel = new MainWindowViewModel(
            null, null, null, null,
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            updateService: updateService,
            applicationVersion: new Version(0, 0, 1, 0));

        await viewModel.CheckForUpdateAsync();

        Assert.IsFalse(viewModel.IsUpdateAvailable);
        Assert.AreEqual(string.Empty, viewModel.UpdateNotificationMessage);
    }

    [TestMethod]
    public void RunningState_DisablesAvailableActions()
    {
        var viewModel = CreateConfiguredViewModel();

        viewModel.IsRunning = true;

        Assert.IsFalse(viewModel.AreControlsEnabled);
        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.IsFalse(viewModel.IsTestActionEnabled);
        Assert.IsFalse(viewModel.ConfigureCredentialsCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task AccessPamAsync_ConfiguredCredentials_OpensIndependentAuthenticatedBrowser()
    {
        var interactiveAccess = new FakeInteractiveAccessService();
        var dialogs = new FakeDialogs(confirm: true);
        var viewModel = new MainWindowViewModel(
            null,
            null,
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            interactiveAccessService: interactiveAccess)
        {
            AreCredentialsConfigured = true
        };

        Assert.IsTrue(viewModel.AccessPamCommand.CanExecute(null));

        await viewModel.AccessPamCommand.ExecuteAsync(null);

        Assert.AreEqual(1, interactiveAccess.OpenCount);
        Assert.IsFalse(viewModel.IsAccessingPam);
        Assert.Contains("sesión PAM se inició correctamente", viewModel.StatusMessage);
        Assert.IsNull(dialogs.LastError);

    }

    [TestMethod]
    public async Task AccessPamAsync_AuthenticationRejected_RestoresControlsAndShowsSafeError()
    {
        var interactiveAccess = new FakeInteractiveAccessService
        {
            Failure = new PamAuthenticationException(PamAuthenticationErrorCode.CredentialsRejected)
        };
        var dialogs = new FakeDialogs(confirm: true);
        var viewModel = new MainWindowViewModel(
            null,
            null,
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            interactiveAccessService: interactiveAccess)
        {
            AreCredentialsConfigured = true
        };

        await viewModel.AccessPamCommand.ExecuteAsync(null);

        Assert.IsFalse(viewModel.IsAccessingPam);
        Assert.IsTrue(viewModel.AreControlsEnabled);
        Assert.Contains("rechazó las credenciales", viewModel.StatusMessage);
        Assert.AreEqual(viewModel.StatusMessage, dialogs.LastError);
    }

    [TestMethod]
    public async Task AccessPamAsync_WhileRequestAutomationRuns_RemainsAvailableAndPreservesProgressStatus()
    {
        var interactiveAccess = new FakeInteractiveAccessService();
        var viewModel = new MainWindowViewModel(
            null,
            null,
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            interactiveAccessService: interactiveAccess)
        {
            AreCredentialsConfigured = true,
            IsRequestConfigured = true,
            IsRunning = true,
            StatusMessage = "Se solicitó NOSQLBOOSTER."
        };

        Assert.IsTrue(viewModel.AccessPamCommand.CanExecute(null));

        await viewModel.AccessPamCommand.ExecuteAsync(null);

        Assert.AreEqual(1, interactiveAccess.OpenCount);
        Assert.IsTrue(viewModel.IsRunning);
        Assert.AreEqual("Se solicitó NOSQLBOOSTER.", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task AccessPamAsync_ConcurrentFailure_NotifiesWithoutReplacingAutomationProgress()
    {
        var interactiveAccess = new FakeInteractiveAccessService
        {
            Failure = new PamAuthenticationException(PamAuthenticationErrorCode.ConnectionOrCertificateFailure)
        };
        var dialogs = new FakeDialogs(confirm: true);
        var viewModel = new MainWindowViewModel(
            null,
            null,
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            interactiveAccessService: interactiveAccess)
        {
            AreCredentialsConfigured = true,
            IsRequestConfigured = true,
            IsRunning = true,
            StatusMessage = "Se solicitó NOSQLBOOSTER."
        };

        await viewModel.AccessPamCommand.ExecuteAsync(null);

        Assert.IsNotNull(dialogs.LastError);
        Assert.AreEqual("Se solicitó NOSQLBOOSTER.", viewModel.StatusMessage);
        Assert.IsTrue(viewModel.IsRunning);
    }

    [TestMethod]
    public void PresentSubmissionProgress_SubmittedApplication_ShowsApplicationColumnName()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.PresentSubmissionProgress(new PamRequestSubmissionProgress(
            new PamFavoriteAccount(
                "system-a",
                "PLSQL - Nodo1",
                "account-a"),
            PamRequestSubmissionStatus.Submitted,
            1,
            5));

        Assert.AreEqual("Se solicitó PLSQL - Nodo1.", viewModel.StatusMessage);
    }

    [TestMethod]
    public void TestAction_RequiresTestModeAndCompleteConfiguration()
    {
        var viewModel = CreateConfiguredViewModel();

        Assert.IsFalse(viewModel.IsTestActionVisible);

        viewModel.IsTestModeEnabled = true;

        Assert.IsTrue(viewModel.IsTestActionVisible);
        Assert.IsTrue(viewModel.IsTestActionEnabled);

        viewModel.SendEmailAtEnd = true;

        Assert.IsFalse(viewModel.IsConfigurationComplete);
        Assert.IsFalse(viewModel.IsTestActionVisible);

        viewModel.IsMailConfigured = true;

        Assert.IsTrue(viewModel.IsConfigurationComplete);
        Assert.IsTrue(viewModel.IsTestActionVisible);
    }

    [TestMethod]
    public async Task RealExecution_WhenConfirmed_ExposesResultWithoutOpeningAutomaticDialog()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(1, context.Execution.PreparationCount);
        Assert.AreEqual(1, context.Execution.RealExecutionCount);
        Assert.IsTrue(context.ViewModel.HasResult);
        Assert.IsTrue(context.ViewModel.HasRequestIds);
        Assert.Contains("Request ID: 2", context.ViewModel.ResultSummary);
        Assert.AreEqual("8001" + Environment.NewLine + "8002", context.ViewModel.RequestIdsText);
        Assert.IsFalse(context.ViewModel.IsRunning);
        Assert.AreEqual(0, context.Dialogs.ResultDisplayCount);

        context.ViewModel.ShowResultCommand.Execute(null);

        Assert.AreEqual(1, context.Dialogs.ResultDisplayCount);
        Assert.Contains("8001" + Environment.NewLine + "8002", context.Dialogs.LastResultMessage);
    }

    [TestMethod]
    public async Task RealExecution_WhenConfirmationIsRejected_DoesNotExecute()
    {
        TestContext context = CreateIntegratedViewModel(confirm: false);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(0, context.Execution.PreparationCount);
        Assert.AreEqual(0, context.Execution.RealExecutionCount);
        Assert.Contains("No se inició ninguna validación", context.ViewModel.StatusMessage);
        Assert.IsFalse(context.ViewModel.HasResult);
    }

    [TestMethod]
    public async Task RealExecution_ExactPendingDuplicatesRejected_CancelsBeforeSubmission()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        context.Execution.PendingDuplicateCount = 2;
        context.Dialogs.ConfirmRepeatedRequests = false;
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(1, context.Execution.PreparationCount);
        Assert.AreEqual(1, context.Dialogs.RepeatedPendingConfirmationCount);
        Assert.AreEqual(0, context.Execution.RealExecutionCount);
        Assert.Contains("no se volvieron a crear", context.ViewModel.StatusMessage);
    }

    [TestMethod]
    public async Task RealExecution_ExactPendingDuplicatesAccepted_SubmitsAgain()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        context.Execution.PendingDuplicateCount = 2;
        context.Dialogs.ConfirmRepeatedRequests = true;
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(1, context.Execution.PreparationCount);
        Assert.AreEqual(1, context.Dialogs.RepeatedPendingConfirmationCount);
        Assert.AreEqual(1, context.Execution.RealExecutionCount);
    }

    [TestMethod]
    public async Task UnknownFailure_IsNotExposedInUserMessage()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        context.Execution.Failure = new InvalidOperationException("internal-sensitive-detail");
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.DoesNotContain("internal-sensitive-detail", context.ViewModel.StatusMessage);
        Assert.Contains("no esperado", context.ViewModel.StatusMessage);
    }

    [TestMethod]
    public async Task UnknownFailure_WithDiagnosticsAvailable_SuggestsSafeExport()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true, configureDiagnostics: true);
        context.Execution.Failure = new InvalidOperationException("internal-sensitive-detail");
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.Contains("Exportar diagnóstico", context.ViewModel.StatusMessage);
        Assert.DoesNotContain("internal-sensitive-detail", context.ViewModel.StatusMessage);
        Assert.AreEqual(context.ViewModel.StatusMessage, context.Dialogs.LastError);
    }

    [TestMethod]
    public async Task ExportDiagnostics_WhenPathSelected_ExportsOnlySafeSummary()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true, configureDiagnostics: true);
        await context.ViewModel.InitializeAsync();
        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        await context.ViewModel.ExportDiagnosticsCommand.ExecuteAsync(null);

        Assert.AreEqual("C:\\support\\diagnostic.zip", context.Diagnostics.LastPath);
        Assert.IsNotNull(context.Diagnostics.LastContext);
        Assert.AreEqual(2, context.Diagnostics.LastContext.LastExecution?.RequestIdCount);
        Assert.IsFalse(context.ViewModel.IsExportingDiagnostics);
        Assert.Contains("exportado", context.ViewModel.StatusMessage);
    }

    [TestMethod]
    public async Task ExportDiagnostics_WhenPickerIsCancelled_DoesNotCreateExport()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true, configureDiagnostics: true);
        context.Dialogs.DiagnosticPath = null;

        await context.ViewModel.ExportDiagnosticsCommand.ExecuteAsync(null);

        Assert.AreEqual(0, context.Diagnostics.ExportCount);
        Assert.IsFalse(context.ViewModel.IsExportingDiagnostics);
    }

    [TestMethod]
    public async Task ExportDiagnostics_WhenExporterFails_RestoresControlsAndShowsSafeError()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true, configureDiagnostics: true);
        context.Diagnostics.Failure = new IOException("private-path-detail");

        await context.ViewModel.ExportDiagnosticsCommand.ExecuteAsync(null);

        Assert.IsFalse(context.ViewModel.IsExportingDiagnostics);
        Assert.IsTrue(context.ViewModel.AreControlsEnabled);
        Assert.Contains("No fue posible exportar", context.ViewModel.StatusMessage);
        Assert.DoesNotContain("private-path-detail", context.ViewModel.StatusMessage);
        Assert.AreEqual(context.ViewModel.StatusMessage, context.Dialogs.LastError);
    }

    [TestMethod]
    public async Task ToggleFinalAction_PersistsSelectedAction()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.ToggleFinalActionCommand.ExecuteAsync(null);

        Assert.IsTrue(context.ViewModel.SendEmailAtEnd);
        Assert.AreEqual(FinalAction.SendEmail, context.SettingsStore.LastSaved?.FinalAction);
    }

    [TestMethod]
    public async Task TestEmail_WhenFinalConfirmationIsRejected_DoesNotSend()
    {
        TestContext context = CreateIntegratedViewModel(confirm: false, configureTestEmail: true);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartTestCommand.ExecuteAsync(null);

        Assert.AreEqual(0, context.Execution.EmailSendCount);
        Assert.Contains("cancelado", context.ViewModel.ResultSummary);
    }

    [TestMethod]
    public async Task TestEmail_WhenConfirmed_UsesCanonicalIds()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true, configureTestEmail: true);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartTestCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Execution.EmailSendCount);
        CollectionAssert.AreEqual(PamRequestTestResultIds.Values.ToArray(), context.Execution.LastEmailIds);
        Assert.IsTrue(context.Execution.LastEmailWasTest);
    }

    [TestMethod]
    public async Task InitializeAsync_LegacyMailSettings_MigratesOutlookAccountSeparately()
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        var accountStore = new FakeOutlookAccountSettingsStore();
        var viewModel = new MainWindowViewModel(
            new FakeExecutionService(now),
            new FakeSettingsStore(new AppSettings()),
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()),
            mailSettingsStore: new FakeMailSettingsStore(),
            outlookAccountSettingsStore: accountStore);

        await viewModel.InitializeAsync();

        Assert.AreEqual("sender@example.test", accountStore.LastSaved?.SmtpAddress);
    }

    private static MainWindowViewModel CreateConfiguredViewModel()
    {
        return new MainWindowViewModel
        {
            AreCredentialsConfigured = true,
            IsRequestConfigured = true
        };
    }

    private static TestContext CreateIntegratedViewModel(
        bool confirm,
        bool configureTestEmail = false,
        bool configureDiagnostics = false)
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        var settings = new AppSettings
        {
            FinalAction = configureTestEmail ? FinalAction.SendEmail : FinalAction.DisplayRequestIds,
            TestModeEnabled = configureTestEmail,
            Request = new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 10),
                StartTime = new TimeOnly(17, 0),
                SessionHours = 1,
                RequestReason = "Prueba de interfaz"
            }
        };
        var execution = new FakeExecutionService(now);
        var settingsStore = new FakeSettingsStore(settings);
        var dialogs = new FakeDialogs(confirm);
        var diagnostics = new FakeDiagnosticExportService();
        var viewModel = new MainWindowViewModel(
            execution,
            settingsStore,
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()),
            diagnosticExportService: configureDiagnostics ? diagnostics : null);
        if (configureTestEmail)
        {
            viewModel = new MainWindowViewModel(
                execution, settingsStore, new FakeCredentialStore(), dialogs,
                new AppSettingsValidator(new RequestSettingsValidator()), new FixedTimeProvider(now.ToUniversalTime()),
                mailSettingsStore: new FakeMailSettingsStore(),
                diagnosticExportService: configureDiagnostics ? diagnostics : null);
        }
        return new TestContext(viewModel, execution, settingsStore, dialogs, diagnostics);
    }

    private sealed record TestContext(
        MainWindowViewModel ViewModel,
        FakeExecutionService Execution,
        FakeSettingsStore SettingsStore,
        FakeDialogs Dialogs,
        FakeDiagnosticExportService Diagnostics);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeSettingsStore(AppSettings settings) : IAppSettingsStore
    {
        public AppSettings? LastSaved { get; private set; }
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task SaveAsync(AppSettings value, CancellationToken cancellationToken = default)
        {
            LastSaved = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMailSettingsStore : IMailSettingsStore
    {
        private static readonly MailSettings Settings = new()
        {
            SmtpAddress = "sender@example.test",
            ToRecipients = ["recipient@example.test"],
            Subject = "Results",
            HtmlTemplate = "<p>{{REQUEST_IDS}}</p>"
        };
        public bool Exists() => true;
        public Task<MailSettings?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<MailSettings?>(Settings);
        public Task SaveAsync(MailSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeOutlookAccountSettingsStore : IOutlookAccountSettingsStore
    {
        public OutlookAccountSettings? LastSaved { get; private set; }
        public bool Exists() => LastSaved is not null;
        public Task<OutlookAccountSettings?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<OutlookAccountSettings?>(null);
        public Task SaveAsync(OutlookAccountSettings settings, CancellationToken cancellationToken = default)
        {
            LastSaved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutlookClient : IOutlookClassicClient
    {
        public Task<OutlookAccount> ResolveAccountAsync(string smtpAddress, CancellationToken cancellationToken = default) => Task.FromResult(new OutlookAccount(smtpAddress));
        public Task ValidateAccountAsync(string smtpAddress, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<OutlookExistingMessageStatus> FindExistingAsync(string smtpAddress, string subject, IReadOnlyList<string> requestIds, CancellationToken cancellationToken = default) => Task.FromResult(OutlookExistingMessageStatus.None);
        public Task<OutlookDeliveryResult> SendAsync(OutlookSendRequest request, CancellationToken cancellationToken = default) => Task.FromResult(OutlookDeliveryResult.SubmittedUnconfirmed());
    }

    private sealed class FakeCredentialStore : IPamCredentialStore
    {
        public bool HasCredentials() => true;
        public PamCredentials? LoadForAuthentication() => new("user", "password", "JBSWY3DPEHPK3PXP");
        public void Save(PamCredentialUpdate update) => throw new NotSupportedException();
        public void Delete() => throw new NotSupportedException();
    }

    private sealed class FakeDialogs : IUserDialogService
    {
        private readonly bool confirm;

        public FakeDialogs(bool confirm)
        {
            this.confirm = confirm;
            ConfirmRepeatedRequests = confirm;
        }

        public string? DiagnosticPath { get; set; } = "C:\\support\\diagnostic.zip";
        public string? LastError { get; private set; }
        public int ResultDisplayCount { get; private set; }
        public string LastResultMessage { get; private set; } = string.Empty;
        public bool ConfirmRepeatedRequests { get; set; }
        public int AutomationStartConfirmationCount { get; private set; }
        public int RepeatedPendingConfirmationCount { get; private set; }
        public bool ConfirmRepeatedPendingRequests(PamExecutionPreparation preparation)
        {
            RepeatedPendingConfirmationCount++;
            return ConfirmRepeatedRequests;
        }
        public bool ConfirmAutomationStart(AppSettings settings, MailSettings? mailSettings)
        {
            AutomationStartConfirmationCount++;
            return confirm;
        }
        public bool ConfirmTestEmail(string recipient) => confirm;
        public bool ConfirmOutlookTestEmail(string recipient) => confirm;
        public void ShowResult(string title, string message)
        {
            ResultDisplayCount++;
            LastResultMessage = message;
        }
        public void ShowError(string message) => LastError = message;
        public string? SelectDiagnosticExportPath(string suggestedFileName) => DiagnosticPath;
    }

    private sealed class FakeDiagnosticExportService : IDiagnosticExportService
    {
        public Exception? Failure { get; set; }
        public int ExportCount { get; private set; }
        public string? LastPath { get; private set; }
        public DiagnosticExportContext? LastContext { get; private set; }

        public Task ExportAsync(
            string destinationPath,
            DiagnosticExportContext context,
            CancellationToken cancellationToken = default)
        {
            ExportCount++;
            LastPath = destinationPath;
            LastContext = context;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeUpdateService : IApplicationUpdateService
    {
        private readonly ApplicationUpdateResult? result;
        private readonly Exception? failure;

        public FakeUpdateService(ApplicationUpdateResult result) => this.result = result;
        public FakeUpdateService(Exception failure) => this.failure = failure;

        public Task<ApplicationUpdateResult> CheckAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default) => failure is null
                ? Task.FromResult(result!)
                : Task.FromException<ApplicationUpdateResult>(failure);
    }

    private sealed class FakeInteractiveAccessService : IPamInteractiveAccessService
    {
        public int OpenCount { get; private set; }
        public Exception? Failure { get; init; }

        public Task OpenAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Failure is null
                ? Task.CompletedTask
                : Task.FromException(Failure);
        }
    }

    private sealed class FakeExecutionService(DateTimeOffset now) : IPamAutomationExecutionService
    {
        public int RealExecutionCount { get; private set; }
        public int PreparationCount { get; private set; }
        public Exception? Failure { get; set; }
        public int EmailSendCount { get; private set; }
        public string[] LastEmailIds { get; private set; } = [];
        public bool LastEmailWasTest { get; private set; }
        public int PendingDuplicateCount { get; set; }

        public Task<PamExecutionPreparation> PrepareRealAsync(PamCredentials credentials, AppSettings settings,
            DateTimeOffset ecuadorNow, CancellationToken cancellationToken = default)
        {
            PreparationCount++;
            return Task.FromResult(new PamExecutionPreparation(
                2,
                settings.Request,
                settings.FinalAction,
                PendingDuplicateCount > 0 ? ["7001"] : [],
                PendingDuplicateCount));
        }

        public Task<PamExecutionResult> ExecuteRealAsync(PamCredentials credentials,
            PamExecutionPreparation preparation, DateTimeOffset ecuadorNow,
            MailSettings? mailSettings = null,
            CancellationToken cancellationToken = default,
            IProgress<PamRequestSubmissionProgress>? progress = null)
        {
            RealExecutionCount++;
            if (Failure is not null) return Task.FromException<PamExecutionResult>(Failure);
            return Task.FromResult(new PamExecutionResult(
                PamExecutionMode.Real, now, FinalAction.DisplayRequestIds, 2, 2, 0, ["8001", "8002"]));
        }

        public Task<PamExecutionResult> ExecuteTestAsync(PamCredentials credentials, AppSettings settings,
            DateTimeOffset ecuadorNow, MailSettings? mailSettings = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PamExecutionResult(
                PamExecutionMode.Test, now, settings.FinalAction, 2, 2, 0, []));

        public Task<OutlookDeliveryResult> SendResultEmailAsync(MailSettings mailSettings,
            IReadOnlyList<string> requestIds, bool isTest, CancellationToken cancellationToken = default)
        {
            EmailSendCount++;
            LastEmailIds = requestIds.ToArray();
            LastEmailWasTest = isTest;
            return Task.FromResult(OutlookDeliveryResult.SubmittedUnconfirmed());
        }
    }
}
