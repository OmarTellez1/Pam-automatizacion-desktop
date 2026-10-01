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
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Interfaces.Updates;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Updates;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests;

[TestClass]
public sealed partial class MainWindowViewModelTests
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
        Assert.AreEqual("PAM Automation", viewModel.ApplicationDisplayName);
    }

    [TestMethod]
    public void BeginInitialization_DisablesActionsAndShowsLoadingState()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.BeginInitialization();

        Assert.IsTrue(viewModel.IsInitializing);
        Assert.IsTrue(viewModel.IsBusy);
        Assert.IsFalse(viewModel.AreControlsEnabled);
        Assert.AreEqual("Preparando PAM Automation…", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task InitializeAsync_AfterLoadingState_RestoresActions()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.BeginInitialization();

        await viewModel.InitializeAsync();

        Assert.IsFalse(viewModel.IsInitializing);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.AreControlsEnabled);
    }

    [TestMethod]
    public async Task InitializeAsync_RequestStartTimeInPast_AdvancesAndPersistsFirstValidMinute()
    {
        var now = new DateTimeOffset(2026, 9, 11, 17, 30, 45, TimeSpan.FromHours(-5));
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
        var settingsStore = new FakeSettingsStore(settings);
        var viewModel = new MainWindowViewModel(
            new FakeExecutionService(now),
            settingsStore,
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()),
            favoriteCollectionService: CreateActiveCollectionService(settings));

        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.CanStartAutomation);
        Assert.IsFalse(viewModel.CanAccessPam);
        Assert.IsFalse(viewModel.IsStartAutomationWarningVisible);
        Assert.AreEqual(new DateOnly(2026, 9, 11), settingsStore.LastSaved?.Request.StartDate);
        Assert.AreEqual(new TimeOnly(17, 37), settingsStore.LastSaved?.Request.StartTime);
        Assert.Contains("se actualizaron automáticamente", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task InitializeAsync_FutureRequestStart_DoesNotRewriteSettings()
    {
        var now = new DateTimeOffset(2026, 9, 11, 17, 30, 0, TimeSpan.FromHours(-5));
        AppSettings settings = CreateSettingsForCollectionTest("Soporte", new TimeOnly(18, 0));
        settings = settings with
        {
            Request = settings.Request with { StartDate = new DateOnly(2026, 9, 11) }
        };
        var settingsStore = new FakeSettingsStore(settings);
        var viewModel = new MainWindowViewModel(
            new FakeExecutionService(now),
            settingsStore,
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()),
            favoriteCollectionService: CreateActiveCollectionService(settings));

        await viewModel.InitializeAsync();

        Assert.IsNull(settingsStore.LastSaved);
        Assert.IsTrue(viewModel.CanStartAutomation);
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
    public async Task RefreshTimeSensitiveState_WhenLeadTimeFallsBelowMinimum_DisablesAutomationAndShowsReason()
    {
        var localNow = new DateTimeOffset(2026, 9, 12, 20, 25, 0, TimeSpan.FromHours(-5));
        var timeProvider = new MutableTimeProvider(localNow.ToUniversalTime());
        TestContext context = CreateTimeSensitiveViewModel(timeProvider, localNow.Date, new TimeOnly(20, 32));
        await context.ViewModel.InitializeAsync();
        Assert.IsTrue(context.ViewModel.CanStartAutomation);

        timeProvider.UtcNow = new DateTimeOffset(2026, 9, 13, 1, 27, 0, TimeSpan.Zero);
        context.ViewModel.RefreshTimeSensitiveState();

        Assert.IsFalse(context.ViewModel.CanStartAutomation);
        Assert.IsTrue(context.ViewModel.IsStartAutomationWarningVisible);
        Assert.Contains("al menos 6 minutos", context.ViewModel.StartAutomationWarningMessage);
        Assert.Contains("6 minutos de anticipación", context.ViewModel.StatusMessage);
    }

    [TestMethod]
    public async Task StartAutomation_WhenTimeExpiredBeforeConfirmation_BlocksWithoutPreparingPam()
    {
        var localNow = new DateTimeOffset(2026, 9, 12, 20, 25, 0, TimeSpan.FromHours(-5));
        var timeProvider = new MutableTimeProvider(localNow.ToUniversalTime());
        TestContext context = CreateTimeSensitiveViewModel(timeProvider, localNow.Date, new TimeOnly(20, 32));
        await context.ViewModel.InitializeAsync();
        timeProvider.UtcNow = new DateTimeOffset(2026, 9, 13, 1, 27, 0, TimeSpan.Zero);

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(0, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(0, context.Execution.PreparationCount);
        string? error = context.Dialogs.LastError;
        Assert.IsNotNull(error);
        Assert.Contains("al menos 6 minutos", error!);
    }

    [TestMethod]
    public async Task StartAutomation_WhenTimeExpiresInsideConfirmation_CancelsBeforePam()
    {
        var localNow = new DateTimeOffset(2026, 9, 12, 20, 25, 0, TimeSpan.FromHours(-5));
        var timeProvider = new MutableTimeProvider(localNow.ToUniversalTime());
        TestContext context = CreateTimeSensitiveViewModel(timeProvider, localNow.Date, new TimeOnly(20, 32));
        context.Dialogs.AutomationStartConfirmationAction = () =>
            timeProvider.UtcNow = new DateTimeOffset(2026, 9, 13, 1, 27, 0, TimeSpan.Zero);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(0, context.Execution.PreparationCount);
        string? error = context.Dialogs.LastError;
        Assert.IsNotNull(error);
        Assert.Contains("menos de 6 minutos", error!);
        Assert.Contains("antes de contactar PAM", error!);
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
    public async Task AccessPamAsync_SecondaryIdentitySelected_UsesOnlySecondaryCredential()
    {
        var principal = new PamAccountSummary(PamAccountId.New(), "Principal", true);
        var secondary = new PamAccountSummary(PamAccountId.New(), "Pepe", false);
        var accountService = new FakePamAccountService(
            [new PamAccountStatus(principal, true), new PamAccountStatus(secondary, true)],
            new Dictionary<PamAccountId, PamCredentials>
            {
                [principal.Id] = new("principal", "principal-password", "JBSWY3DPEHPK3PXP"),
                [secondary.Id] = new("secondary", "secondary-password", "JBSWY3DPEHPK3PXP")
            });
        var configurationDialogs = new FakeConfigurationDialogs { SelectedAccountId = secondary.Id };
        var interactiveAccess = new FakeInteractiveAccessService();
        var viewModel = new MainWindowViewModel(
            null,
            new FakeSettingsStore(new AppSettings()),
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            configurationDialogs: configurationDialogs,
            interactiveAccessService: interactiveAccess,
            pamAccountService: accountService);
        await viewModel.InitializeAsync();

        Assert.AreEqual("Principal", viewModel.PrincipalAccountName);
        Assert.AreEqual("P", viewModel.PrincipalAccountInitial);
        await viewModel.AccessPamCommand.ExecuteAsync(null);

        Assert.AreEqual(1, configurationDialogs.SelectionCount);
        Assert.AreEqual(secondary.Id, accountService.LastLoadedAccountId);
        Assert.AreEqual("secondary", interactiveAccess.LastCredentials?.Username);
    }

    [TestMethod]
    public async Task AccessPamAsync_SelectionCancelled_DoesNotLoadCredentialsOrOpenBrowser()
    {
        var principal = new PamAccountSummary(PamAccountId.New(), "Principal", true);
        var accountService = new FakePamAccountService(
            [new PamAccountStatus(principal, true)],
            new Dictionary<PamAccountId, PamCredentials>
            {
                [principal.Id] = new("principal", "password", "JBSWY3DPEHPK3PXP")
            });
        var configurationDialogs = new FakeConfigurationDialogs();
        var interactiveAccess = new FakeInteractiveAccessService();
        var viewModel = new MainWindowViewModel(
            null,
            new FakeSettingsStore(new AppSettings()),
            new FakeCredentialStore(),
            new FakeDialogs(confirm: true),
            new AppSettingsValidator(new RequestSettingsValidator()),
            TimeProvider.System,
            configurationDialogs: configurationDialogs,
            interactiveAccessService: interactiveAccess,
            pamAccountService: accountService);
        await viewModel.InitializeAsync();

        await viewModel.AccessPamCommand.ExecuteAsync(null);

        Assert.AreEqual(1, configurationDialogs.SelectionCount);
        Assert.IsNull(accountService.LastLoadedAccountId);
        Assert.AreEqual(0, interactiveAccess.OpenCount);
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
    public async Task TestAction_RequiresTestModeAndCompleteConfiguration()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        MainWindowViewModel viewModel = context.ViewModel;
        await viewModel.InitializeAsync();

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
        Assert.AreEqual("8001" + Environment.NewLine + "8002", context.Dialogs.LastCopyRequestIdsText);
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
        context.Dialogs.RepeatedPendingDecision = RepeatedPendingDecision.Cancel;
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(1, context.Dialogs.AutomationStartConfirmationCount);
        Assert.AreEqual(1, context.Execution.PreparationCount);
        Assert.AreEqual(1, context.Dialogs.RepeatedPendingConfirmationCount);
        Assert.AreEqual(0, context.Execution.RealExecutionCount);
        Assert.Contains("Cambia la fecha u hora de inicio, la duración o el motivo", context.ViewModel.StatusMessage);
    }

    [TestMethod]
    public async Task RealExecution_CompleteExactPendingSetSelected_EmailsExistingIdsWithoutSubmission()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true, configureTestEmail: true);
        context.Execution.PendingDuplicateCount = 1;
        context.Execution.ReusablePendingIds = ["7001"];
        context.Dialogs.RepeatedPendingDecision = RepeatedPendingDecision.UseExistingRequestIds;
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.AreEqual(0, context.Execution.RealExecutionCount);
        Assert.AreEqual(1, context.Execution.ExistingPendingCompletionCount);
        Assert.AreEqual("Solicitudes existentes utilizadas", context.ViewModel.ResultTitle);
        Assert.AreEqual("7001", context.ViewModel.RequestIdsText);
        Assert.Contains("Solicitudes nuevas creadas: 0", context.ViewModel.ResultSummary);
        Assert.AreEqual(1, context.Dialogs.ResultDisplayCount);
        Assert.Contains("Outlook recibió correctamente", context.Dialogs.LastResultMessage);
    }

    [TestMethod]
    public async Task ReconciliationFailureAfterSubmission_WarnsToReviewPendingBeforeRetrying()
    {
        TestContext context = CreateIntegratedViewModel(confirm: true);
        context.Execution.Failure = new PamRequestSubmissionException(
            PamRequestSubmissionErrorCode.ReconciliationFailedAfterSubmission,
            processedCount: 10,
            partialRun: new PamRequestRunResult(Enumerable.Range(1, 10).Select(index =>
                new PamRequestAccountResult(new PamFavoriteAccount("fixture-system", "fixture-application", $"fixture-account-{index}"),
                    PamRequestSubmissionStatus.Submitted, null) { ConfirmedInPending = index == 1 }).ToArray()),
            recoveredRequestIds: ["7003"]);
        await context.ViewModel.InitializeAsync();

        await context.ViewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.IsTrue(context.ViewModel.HasResult);
        Assert.AreEqual("Automatización incompleta", context.ViewModel.ResultTitle);
        Assert.Contains("Aceptadas por respuesta: 10", context.ViewModel.ResultSummary);
        Assert.Contains("verificadas en Pending: 1", context.ViewModel.ResultSummary);
        Assert.IsTrue(context.ViewModel.HasRequestIds);
        Assert.AreEqual("7003", context.ViewModel.RequestIdsText);
        Assert.Contains("Revisa Pending", context.ViewModel.StatusMessage);
        Assert.Contains("Se conservan los resultados conocidos", context.ViewModel.StatusMessage);
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
    public async Task Automation_ActiveCollection_UsesGlobalScheduleAndCollectionReasonAndSelection()
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        AppSettings globalSettings = CreateSettingsForCollectionTest("Configuración global", new TimeOnly(17, 0));
        var collection = new FavoriteCollection(
            Guid.NewGuid(),
            "Accesos Peter",
            "Configuración de colección",
            [new FavoriteAccountKey("system", "application", "account")]);
        var collectionService = new FakeFavoriteCollectionService(
            new FavoriteCollectionsState([collection], collection.Id));
        var execution = new FakeExecutionService(now);
        var dialogs = new FakeDialogs(confirm: true);
        var principal = new PamAccountSummary(PamAccountId.New(), "Principal", true);
        var secondary = new PamAccountSummary(PamAccountId.New(), "Secundario", false);
        var accountService = new FakePamAccountService(
            [new PamAccountStatus(principal, true), new PamAccountStatus(secondary, true)],
            new Dictionary<PamAccountId, PamCredentials>
            {
                [principal.Id] = new("principal", "password", "JBSWY3DPEHPK3PXP"),
                [secondary.Id] = new("secondary", "password", "JBSWY3DPEHPK3PXP")
            });
        var configurationDialogs = new FakeConfigurationDialogs { SelectedAccountId = secondary.Id };
        var viewModel = new MainWindowViewModel(
            execution,
            new FakeSettingsStore(globalSettings),
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()),
            configurationDialogs: configurationDialogs,
            favoriteCollectionService: collectionService,
            pamAccountService: accountService);
        await viewModel.InitializeAsync();

        await viewModel.StartAutomationCommand.ExecuteAsync(null);

        Assert.Contains("Accesos Peter", viewModel.ActiveCollectionText);
        Assert.AreEqual("Accesos Peter", dialogs.LastAutomationCollectionName);
        Assert.AreEqual(1, execution.CollectionPreparationCount);
        Assert.AreEqual(new TimeOnly(17, 0), execution.LastPreparedSettings?.Request.StartTime);
        Assert.AreEqual("Configuración de colección", execution.LastPreparedSettings?.Request.RequestReason);
        Assert.HasCount(1, execution.LastSelectedAccounts);
        Assert.AreEqual("user", execution.LastPreparedCredentialUsername);
        Assert.AreEqual(0, configurationDialogs.SelectionCount);
    }

    [TestMethod]
    public async Task Initialize_NoActiveCollection_DisablesAutomation()
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        var dialogs = new FakeDialogs(confirm: true);
        var viewModel = new MainWindowViewModel(
            new FakeExecutionService(now),
            new FakeSettingsStore(CreateSettingsForCollectionTest("Global", new TimeOnly(17, 0))),
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()),
            favoriteCollectionService: new FakeFavoriteCollectionService(new FavoriteCollectionsState()));
        await viewModel.InitializeAsync();

        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.Contains("no hay una colección activa", viewModel.StartAutomationWarningMessage);
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

    private static AppSettings CreateSettingsForCollectionTest(string reason, TimeOnly startTime) => new()
    {
        Request = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 10),
            StartTime = startTime,
            SessionHours = 1,
            RequestReason = reason
        }
    };

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CollectionMail_SwitchActive_UsesOwnMailForConfirmationAndBothExecutionRoutes(bool reusePending)
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        AppSettings settings = CreateSettingsForCollectionTest("Reason", new TimeOnly(17, 0)) with { FinalAction = FinalAction.SendEmail };
        FavoriteCollection first = Core.Outlook.CollectionMailComposerTests.Collection("First", "first");
        FavoriteCollection second = Core.Outlook.CollectionMailComposerTests.Collection(FavoriteCollection.AllFavoritesName, "second");
        var collections = new FakeFavoriteCollectionService(new FavoriteCollectionsState([first, second], first.Id));
        var signatures = new FakeSignatureStore();
        var configuration = new FakeConfigurationDialogs();
        var dialogs = new FakeDialogs(true) { RepeatedPendingDecision = RepeatedPendingDecision.UseExistingRequestIds };
        var execution = new FakeExecutionService(now) { PendingDuplicateCount = reusePending ? 1 : 0,
            ReusablePendingIds = reusePending ? ["7001"] : [] };
        var viewModel = new MainWindowViewModel(execution, new FakeSettingsStore(settings), new FakeCredentialStore(), dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()), new FixedTimeProvider(now.ToUniversalTime()),
            configurationDialogs: configuration, mailSettingsStore: new FakeMailSettingsStore(),
            favoriteCollectionService: collections, mailSignatureStore: signatures);
        await viewModel.InitializeAsync();
        await viewModel.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual("first subject", execution.LastExecutionMail!.Subject);
        Assert.AreEqual("first@example.com", dialogs.LastConfirmationMail!.ToRecipients[0]);
        Assert.AreEqual("first.cc@example.com", dialogs.LastConfirmationMail.CcRecipients[0]);
        Assert.Contains("Firma inicial", execution.LastExecutionMail.HtmlTemplate);

        configuration.SignatureEdit = new MailSignature { HtmlContent = "<p>Firma actualizada</p>" };
        await viewModel.ConfigureSignatureCommand.ExecuteAsync(null);
        Assert.AreEqual(1, signatures.SaveCount);
        configuration.SignatureEdit = null;
        await viewModel.ConfigureSignatureCommand.ExecuteAsync(null);
        Assert.AreEqual(1, signatures.SaveCount, "Cancelar no debe guardar la firma.");
        collections.State = new FavoriteCollectionsState([first, second], second.Id);
        await viewModel.OpenCollectionsCommand.ExecuteAsync(null);
        await viewModel.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual("sender@example.test", configuration.LastCollectionSender);
        Assert.AreEqual("sender@example.test", execution.LastExecutionMail!.SmtpAddress);
        Assert.AreEqual("second subject", execution.LastExecutionMail.Subject);
        Assert.AreEqual("second@example.com", dialogs.LastConfirmationMail!.ToRecipients[0]);
        Assert.AreEqual("second.cc@example.com", dialogs.LastConfirmationMail.CcRecipients[0]);
        Assert.AreSame(dialogs.LastConfirmationMail, execution.LastExecutionMail);
        string body = OutlookHtmlComposer.Compose(execution.LastExecutionMail.HtmlTemplate, ["7001"]);
        Assert.AreEqual(1, body.Split("Firma actualizada").Length - 1);
        Assert.DoesNotContain("Firma inicial", body);
        Assert.DoesNotContain("first content", body);
        Assert.Contains("second content", body);
        Assert.DoesNotContain("Firma", first.Mail!.HtmlTemplate);
        Assert.DoesNotContain("Firma", second.Mail!.HtmlTemplate);
        Assert.AreEqual(reusePending ? 2 : 0, execution.ExistingPendingCompletionCount);
        Assert.AreEqual(reusePending ? 0 : 2, execution.RealExecutionCount);
        Assert.IsNull(dialogs.LastError);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CollectionMail_MissingOrInvalid_BlocksOnlyWhenOutlookEnabled(bool invalid)
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        var collection = new FavoriteCollection(Guid.NewGuid(), "Needs mail", "Reason",
            [new FavoriteAccountKey("system", "app", "account")], mail: invalid ? new CollectionMailSettings { Subject = "Incomplete" } : null);
        var execution = new FakeExecutionService(now);
        var dialogs = new FakeDialogs(true);
        var viewModel = new MainWindowViewModel(execution,
            new FakeSettingsStore(CreateSettingsForCollectionTest("Reason", new TimeOnly(17, 0)) with { FinalAction = FinalAction.SendEmail }),
            new FakeCredentialStore(), dialogs, new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()), mailSettingsStore: new FakeMailSettingsStore(),
            favoriteCollectionService: new FakeFavoriteCollectionService(new FavoriteCollectionsState([collection], collection.Id)));
        await viewModel.InitializeAsync();
        Assert.IsFalse(viewModel.CanStartAutomation);
        Assert.Contains("Needs mail", viewModel.StartAutomationWarningMessage);
        await viewModel.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(0, execution.PreparationCount);
        Assert.AreEqual(0, dialogs.AutomationStartConfirmationCount);
        viewModel.SendEmailAtEnd = false;
        Assert.IsTrue(viewModel.CanStartAutomation);
        await viewModel.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(1, execution.RealExecutionCount);
        Assert.IsNull(execution.LastExecutionMail);
        Assert.IsTrue(viewModel.HasRequestIds);
    }

    private sealed class FakeSignatureStore : IMailSignatureStore
    {
        private MailSignature signature = new() { HtmlContent = "<p>Firma inicial</p>" };
        public int SaveCount { get; private set; }
        public Task<MailSignature?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<MailSignature?>(signature);
        public Task SaveAsync(MailSignature value, CancellationToken cancellationToken = default)
        {
            signature = value;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private static FakeFavoriteCollectionService CreateActiveCollectionService(AppSettings settings)
    {
        var collection = new FavoriteCollection(
            Guid.NewGuid(),
            "Colección de prueba",
            settings.Request.RequestReason,
            [new FavoriteAccountKey("system", "application", "account")],
            mail: new CollectionMailSettings { ToRecipients = ["recipient@example.test"], Subject = "Results", HtmlTemplate = "<p>{{REQUEST_IDS}}</p>" });
        return new FakeFavoriteCollectionService(new FavoriteCollectionsState([collection], collection.Id));
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
            diagnosticExportService: configureDiagnostics ? diagnostics : null,
            favoriteCollectionService: CreateActiveCollectionService(settings));
        if (configureTestEmail)
        {
            viewModel = new MainWindowViewModel(
                execution, settingsStore, new FakeCredentialStore(), dialogs,
                new AppSettingsValidator(new RequestSettingsValidator()), new FixedTimeProvider(now.ToUniversalTime()),
                mailSettingsStore: new FakeMailSettingsStore(),
                diagnosticExportService: configureDiagnostics ? diagnostics : null,
                favoriteCollectionService: CreateActiveCollectionService(settings));
        }
        return new TestContext(viewModel, execution, settingsStore, dialogs, diagnostics);
    }

    private static TestContext CreateTimeSensitiveViewModel(
        TimeProvider timeProvider,
        DateTime startDate,
        TimeOnly startTime)
    {
        var now = timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(-5));
        var settings = new AppSettings
        {
            Request = new RequestSettings
            {
                StartDate = DateOnly.FromDateTime(startDate),
                StartTime = startTime,
                SessionHours = 1,
                RequestReason = "Prueba de vencimiento"
            }
        };
        var execution = new FakeExecutionService(now);
        var settingsStore = new FakeSettingsStore(settings);
        var dialogs = new FakeDialogs(confirm: true);
        var diagnostics = new FakeDiagnosticExportService();
        var viewModel = new MainWindowViewModel(
            execution,
            settingsStore,
            new FakeCredentialStore(),
            dialogs,
            new AppSettingsValidator(new RequestSettingsValidator()),
            timeProvider,
            favoriteCollectionService: CreateActiveCollectionService(settings));
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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
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
        public Task<OutlookExistingMessageStatus> ObserveDeliveryAsync(string smtpAddress, string subject, IReadOnlyList<string> requestIds, bool includeUnsentFolders, CancellationToken cancellationToken = default) => Task.FromResult(OutlookExistingMessageStatus.None);
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
            RepeatedPendingDecision = RepeatedPendingDecision.Cancel;
        }

        public string? DiagnosticPath { get; set; } = "C:\\support\\diagnostic.zip";
        public string? LastError { get; private set; }
        public int ResultDisplayCount { get; private set; }
        public string LastResultMessage { get; private set; } = string.Empty;
        public string? LastCopyRequestIdsText { get; private set; }
        public RepeatedPendingDecision RepeatedPendingDecision { get; set; }
        public Action? AutomationStartConfirmationAction { get; set; }
        public int AutomationStartConfirmationCount { get; private set; }
        public string? LastAutomationCollectionName { get; private set; }
        public MailSettings? LastConfirmationMail { get; private set; }
        public bool? ConfirmAccumulated { get; set; }
        public int AccumulatedConfirmationCount { get; private set; }
        public bool LastConfirmationAccumulates { get; private set; }
        public int RepeatedPendingConfirmationCount { get; private set; }
        public RepeatedPendingDecision ChooseRepeatedPendingAction(PamExecutionPreparation preparation)
        {
            RepeatedPendingConfirmationCount++;
            return RepeatedPendingDecision;
        }
        public bool ConfirmAutomationStart(
            AppSettings settings,
            MailSettings? mailSettings,
            string? collectionName = null,
            bool accumulate = false)
        {
            AutomationStartConfirmationCount++;
            LastAutomationCollectionName = collectionName;
            LastConfirmationMail = mailSettings;
            LastConfirmationAccumulates = accumulate;
            AutomationStartConfirmationAction?.Invoke();
            return confirm;
        }
        public bool ConfirmTestEmail(string recipient) => confirm;
        public bool ConfirmAccumulatedEmail(string collectionName, int count, MailSettings mailSettings)
        {
            AccumulatedConfirmationCount++;
            LastConfirmationMail = mailSettings;
            return ConfirmAccumulated ?? confirm;
        }
        public bool ConfirmDiscardAccumulated(int count) => ConfirmAccumulated ?? confirm;
        public bool ConfirmCloseAccumulated(int count) => ConfirmAccumulated ?? confirm;
        public bool ConfirmOutlookTestEmail(string recipient) => confirm;
        public void ShowResult(string title, string message, string? requestIdsText = null)
        {
            ResultDisplayCount++;
            LastResultMessage = message;
            LastCopyRequestIdsText = requestIdsText;
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
        public PamCredentials? LastCredentials { get; private set; }

        public Task OpenAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            LastCredentials = credentials;
            return Failure is null
                ? Task.CompletedTask
                : Task.FromException(Failure);
        }
    }

    private sealed class FakePamAccountService(
        IReadOnlyList<PamAccountStatus> accounts,
        IReadOnlyDictionary<PamAccountId, PamCredentials> credentials) : IPamAccountService
    {
        public PamAccountId? LastLoadedAccountId { get; private set; }

        public Task<IReadOnlyList<PamAccountStatus>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts);

        public Task<PamCredentials?> LoadCredentialsAsync(PamAccountId accountId, CancellationToken cancellationToken = default)
        {
            LastLoadedAccountId = accountId;
            credentials.TryGetValue(accountId, out PamCredentials? value);
            return Task.FromResult(value);
        }

        public Task<PamAccountSummary> CreatePrincipalAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> RenameAsync(PamAccountId accountId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> CreateSecondaryAsync(string name, PamCredentialUpdate credentialUpdate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> UpdateSecondaryAsync(PamAccountId accountId, string name, PamCredentialUpdate credentialUpdate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteSecondaryAsync(PamAccountId accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeConfigurationDialogs : IConfigurationDialogService
    {
        public PamAccountId? SelectedAccountId { get; init; }
        public int SelectionCount { get; private set; }

        public PamAccountId? SelectPamAccount(IReadOnlyList<PamAccountStatus> accounts)
        {
            SelectionCount++;
            return SelectedAccountId;
        }

        public Task<CredentialConfigurationState> EditCredentialsAsync(CredentialConfigurationState current, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday) => throw new NotSupportedException();
        public MailSignature? SignatureEdit { get; set; }
        public string? LastCollectionSender { get; private set; }
        public MailSignature? EditSignature(MailSignature? current) => SignatureEdit;
        public void OpenFavoriteCollections(string smtpAddress) => LastCollectionSender = smtpAddress;
        public void OpenPamAccounts() => throw new NotSupportedException();
        public void OpenAbout() => throw new NotSupportedException();
    }

    private sealed class FakeFavoriteCollectionService(FavoriteCollectionsState initial) : IFavoriteCollectionService
    {
        public FavoriteCollectionsState State { get; set; } = initial;

        public Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task<FavoriteCollectionsState> SaveCollectionAsync(
            FavoriteCollection collection,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FavoriteCollectionsState> SetActiveCollectionAsync(
            Guid? collectionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FavoriteCollectionsState> DeleteCollectionAsync(
            Guid collectionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<FavoriteCollectionsState> ReconcileAsync(
            FavoriteCatalogSnapshot catalog,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeExecutionService(DateTimeOffset now) : IPamAutomationExecutionService
    {
        public PamExecutionResult? NextResult { get; set; }
        public OutlookDeliveryResult AccumulatedDelivery { get; set; } = OutlookDeliveryResult.SentConfirmed();
        public Exception? AccumulatedFailure { get; set; }
        public TaskCompletionSource<OutlookDeliveryResult>? PendingMail { get; set; }
        public int ObservationCount { get; private set; }
        public MailSettings? LastAccumulatedMail { get; private set; }
        public int RealExecutionCount { get; private set; }
        public MailSettings? LastExecutionMail { get; private set; }
        public int PreparationCount { get; private set; }
        public Exception? Failure { get; set; }
        public int EmailSendCount { get; private set; }
        public string[] LastEmailIds { get; private set; } = [];
        public bool LastEmailWasTest { get; private set; }
        public int PendingDuplicateCount { get; set; }
        public IReadOnlyList<string> ReusablePendingIds { get; set; } = [];
        public int ExistingPendingCompletionCount { get; private set; }
        public int CollectionPreparationCount { get; private set; }
        public AppSettings? LastPreparedSettings { get; private set; }
        public string? LastPreparedCredentialUsername { get; private set; }
        public IReadOnlyList<FavoriteAccountKey> LastSelectedAccounts { get; private set; } = [];

        public Task<PamExecutionPreparation> PrepareRealAsync(PamCredentials credentials, AppSettings settings,
            DateTimeOffset ecuadorNow, CancellationToken cancellationToken = default)
        {
            PreparationCount++;
            return Task.FromResult(new PamExecutionPreparation(
                2,
                settings.Request,
                settings.FinalAction,
                PendingDuplicateCount > 0 ? ["7001"] : [],
                PendingDuplicateCount,
                reusablePendingRequestIds: ReusablePendingIds));
        }

        public Task<PamExecutionPreparation> PrepareCollectionRealAsync(
            PamCredentials credentials,
            AppSettings settings,
            IReadOnlyList<FavoriteAccountKey> selectedAccounts,
            DateTimeOffset ecuadorNow,
            CancellationToken cancellationToken = default)
        {
            PreparationCount++;
            CollectionPreparationCount++;
            LastPreparedSettings = settings;
            LastPreparedCredentialUsername = credentials.Username;
            LastSelectedAccounts = selectedAccounts.ToArray();
            return Task.FromResult(new PamExecutionPreparation(
                selectedAccounts.Count,
                settings.Request,
                settings.FinalAction,
                PendingDuplicateCount > 0 ? ["7001"] : [],
                PendingDuplicateCount,
                selectedAccounts,
                ReusablePendingIds));
        }

        public Task<PamExecutionResult> ExecuteRealAsync(PamCredentials credentials,
            PamExecutionPreparation preparation, DateTimeOffset ecuadorNow,
            MailSettings? mailSettings = null,
            CancellationToken cancellationToken = default,
            IProgress<PamRequestSubmissionProgress>? progress = null)
        {
            RealExecutionCount++;
            LastExecutionMail = mailSettings;
            if (Failure is not null) return Task.FromException<PamExecutionResult>(Failure);
            return Task.FromResult(NextResult ?? new PamExecutionResult(
                PamExecutionMode.Real, now, FinalAction.DisplayRequestIds, 2, 2, 0, ["8001", "8002"]));
        }

        public Task<PamExecutionResult> ExecuteTestAsync(PamCredentials credentials, AppSettings settings,
            DateTimeOffset ecuadorNow, MailSettings? mailSettings = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PamExecutionResult(
                PamExecutionMode.Test, now, settings.FinalAction, 2, 2, 0, []));

        public Task<PamExecutionResult> CompleteWithExistingPendingAsync(
            PamExecutionPreparation preparation,
            MailSettings mailSettings,
            CancellationToken cancellationToken = default)
        {
            ExistingPendingCompletionCount++;
            LastExecutionMail = mailSettings;
            return Task.FromResult(new PamExecutionResult(
                PamExecutionMode.ExistingPending,
                now,
                FinalAction.SendEmail,
                preparation.FavoriteCount,
                preparation.FavoriteCount,
                0,
                preparation.ReusablePendingRequestIds,
                OutlookDeliveryResult.SubmittedUnconfirmed()));
        }

        public Task<OutlookDeliveryResult> SendResultEmailAsync(MailSettings mailSettings,
            IReadOnlyList<string> requestIds, bool isTest, CancellationToken cancellationToken = default)
        {
            EmailSendCount++;
            LastEmailIds = requestIds.ToArray();
            LastEmailWasTest = isTest;
            return Task.FromResult(OutlookDeliveryResult.SubmittedUnconfirmed());
        }
        public Task<OutlookDeliveryResult> SendAccumulatedEmailAsync(MailSettings mailSettings,
            IReadOnlyList<string> requestIds, CancellationToken cancellationToken = default)
        {
            EmailSendCount++;
            LastEmailIds = requestIds.ToArray();
            LastAccumulatedMail = mailSettings;
            return AccumulatedFailure is not null ? Task.FromException<OutlookDeliveryResult>(AccumulatedFailure)
                : PendingMail?.Task ?? Task.FromResult(AccumulatedDelivery);
        }
        public Task<OutlookDeliveryResult> ObserveResultEmailAsync(MailSettings mailSettings,
            IReadOnlyList<string> requestIds, CancellationToken cancellationToken = default)
        {
            ObservationCount++;
            LastAccumulatedMail = mailSettings;
            LastEmailIds = requestIds.ToArray();
            return AccumulatedFailure is not null ? Task.FromException<OutlookDeliveryResult>(AccumulatedFailure)
                : Task.FromResult(AccumulatedDelivery);
        }
    }
}
