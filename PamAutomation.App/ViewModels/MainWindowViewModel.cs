using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Services;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Interfaces.Execution;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Interfaces.Updates;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Validation;

namespace PamAutomation.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IPamAutomationExecutionService? executionService;
    private readonly IAppSettingsStore? settingsStore;
    private readonly IPamCredentialStore? credentialStore;
    private readonly IUserDialogService? dialogs;
    private readonly IConfigurationDialogService? configurationDialogs;
    private readonly IMailSettingsStore? mailSettingsStore;
    private readonly IMailSignatureStore? mailSignatureStore;
    private MailSignature? mailSignature;
    private string mailConfigurationError = string.Empty;
    private string signatureLoadError = string.Empty;
    private readonly IOutlookAccountSettingsStore? outlookAccountSettingsStore;
    private readonly AppSettingsValidator settingsValidator;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IDiagnosticExportService? diagnosticExportService;
    private readonly IApplicationUpdateService? updateService;
    private readonly IPamInteractiveAccessService? interactiveAccessService;
    private readonly IFavoriteCollectionService? favoriteCollectionService;
    private readonly IPamAccountService? pamAccountService;
    private readonly Version applicationVersion;
    private AppSettings settings = new();
    private MailSettings? mailSettings;
    private string outlookSmtpAddress = string.Empty;
    private readonly SemaphoreSlim settingsSaveGate = new(1, 1);
    private PamExecutionResult? lastExecutionResult;
    private FavoriteCollectionsState favoriteCollectionsState = new();
    private bool hasInteractivePamAccount;

    [ObservableProperty] private bool areCredentialsConfigured;
    [ObservableProperty] private bool isRequestConfigured;
    [ObservableProperty] private bool isMailConfigured;
    [ObservableProperty] private bool isTestModeEnabled;
    [ObservableProperty] private bool sendEmailAtEnd;
    [ObservableProperty] private bool isInitializing;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private bool isExportingDiagnostics;
    [ObservableProperty] private bool isAccessingPam;
    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private bool hasRequestIds;
    [ObservableProperty] private bool isUpdateAvailable;
    [ObservableProperty] private string updateNotificationMessage = string.Empty;
    [ObservableProperty] private string statusMessage = "Completa la configuración para habilitar la automatización.";
    [ObservableProperty] private string resultTitle = string.Empty;
    [ObservableProperty] private string resultSummary = string.Empty;
    [ObservableProperty] private string requestIdsText = string.Empty;
    [ObservableProperty] private string principalAccountName = string.Empty;
    [ObservableProperty] private string principalAccountInitial = "?";

    public MainWindowViewModel()
        : this(null, null, null, null, new AppSettingsValidator(new RequestSettingsValidator()), TimeProvider.System, null)
    {
    }

    public MainWindowViewModel(
        IPamAutomationExecutionService? executionService,
        IAppSettingsStore? settingsStore,
        IPamCredentialStore? credentialStore,
        IUserDialogService? dialogs,
        AppSettingsValidator settingsValidator,
        TimeProvider timeProvider,
        IConfigurationDialogService? configurationDialogs = null,
        IMailSettingsStore? mailSettingsStore = null,
        IOutlookAccountSettingsStore? outlookAccountSettingsStore = null,
        ILogger<MainWindowViewModel>? logger = null,
        IDiagnosticExportService? diagnosticExportService = null,
        IApplicationUpdateService? updateService = null,
        Version? applicationVersion = null,
        IPamInteractiveAccessService? interactiveAccessService = null,
        IFavoriteCollectionService? favoriteCollectionService = null,
        IPamAccountService? pamAccountService = null,
        IMailSignatureStore? mailSignatureStore = null,
        RequestIdAccumulator? accumulator = null)
    {
        this.executionService = executionService;
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        this.dialogs = dialogs;
        this.configurationDialogs = configurationDialogs;
        this.mailSettingsStore = mailSettingsStore;
        this.mailSignatureStore = mailSignatureStore;
        this.accumulator = accumulator ?? new RequestIdAccumulator();
        this.outlookAccountSettingsStore = outlookAccountSettingsStore;
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? NullLogger<MainWindowViewModel>.Instance;
        this.diagnosticExportService = diagnosticExportService;
        this.updateService = updateService;
        this.interactiveAccessService = interactiveAccessService;
        this.favoriteCollectionService = favoriteCollectionService;
        this.pamAccountService = pamAccountService;
        this.applicationVersion = applicationVersion ??
            typeof(MainWindowViewModel).Assembly.GetName().Version ?? new Version(0, 0, 1, 0);

        ConfigureCredentialsCommand = new AsyncRelayCommand(ConfigureCredentialsAsync, () => AreControlsEnabled);
        ConfigureRequestCommand = new AsyncRelayCommand(
            ConfigureRequestAsync,
            () => AreControlsEnabled && favoriteCollectionsState.ActiveCollection is not null);
        ConfigureSignatureCommand = new AsyncRelayCommand(ConfigureSignatureAsync, () => AreControlsEnabled);
        OpenCollectionsCommand = new AsyncRelayCommand(OpenCollectionsAsync, () => AreControlsEnabled && configurationDialogs is not null);
        OpenPamAccountsCommand = new AsyncRelayCommand(OpenPamAccountsAsync, () => AreControlsEnabled && configurationDialogs is not null);
        OpenAboutCommand = new RelayCommand(OpenAbout, () => AreControlsEnabled && configurationDialogs is not null);
        StartTestCommand = new AsyncRelayCommand(StartTestAsync, () => IsTestActionEnabled);
        StartAutomationCommand = new AsyncRelayCommand(StartAutomationAsync, () => CanStartAutomation);
        ToggleFinalActionCommand = new AsyncRelayCommand(ToggleFinalActionAsync, () => CanChangeFinalAction);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync, () => CanExportDiagnostics);
        AccessPamCommand = new AsyncRelayCommand(AccessPamAsync, () => CanAccessPam);
        ShowResultCommand = new RelayCommand(ShowResult, () => CanShowResult);
        DismissUpdateCommand = new RelayCommand(() => IsUpdateAvailable = false);
        InitializeAccumulationCommands();
    }

    public IAsyncRelayCommand ConfigureCredentialsCommand { get; }
    public IRelayCommand ConfigureRequestCommand { get; }
    public IAsyncRelayCommand ConfigureSignatureCommand { get; }
    public IAsyncRelayCommand OpenCollectionsCommand { get; }
    public IAsyncRelayCommand OpenPamAccountsCommand { get; }
    public IRelayCommand OpenAboutCommand { get; }
    public IAsyncRelayCommand StartTestCommand { get; }
    public IAsyncRelayCommand StartAutomationCommand { get; }
    public IAsyncRelayCommand ToggleFinalActionCommand { get; }
    public IAsyncRelayCommand ExportDiagnosticsCommand { get; }
    public IAsyncRelayCommand AccessPamCommand { get; }
    public IRelayCommand ShowResultCommand { get; }
    public IRelayCommand DismissUpdateCommand { get; }
    public string ApplicationDisplayName => "PAM Automation";
    public bool AreControlsEnabled => !IsInitializing && !IsRunning && !IsExportingDiagnostics && !IsAccessingPam;
    public bool IsBusy => IsInitializing || IsRunning || IsAccessingPam;
    public bool CanExportDiagnostics => diagnosticExportService is not null && dialogs is not null && AreControlsEnabled;
    public bool CanAccessPam => interactiveAccessService is not null && !IsInitializing && !IsAccessingPam &&
                                !IsExportingDiagnostics &&
                                (pamAccountService is not null
                                    ? configurationDialogs is not null && hasInteractivePamAccount
                                    : credentialStore is not null && AreCredentialsConfigured);
    public bool CanShowResult => dialogs is not null && HasResult && AreControlsEnabled;
    public bool IsEffectiveRequestConfigured => favoriteCollectionsState.ActiveCollection is { Accounts.Count: > 0 } &&
                                                settingsValidator.Validate(GetEffectiveSettings(), GetEcuadorNow()).IsValid;
    public bool IsConfigurationComplete => AreCredentialsConfigured && IsEffectiveRequestConfigured && (IsAccumulating || !SendEmailAtEnd || IsMailConfigured);
    public bool CanStartAutomation => AreControlsEnabled && IsConfigurationComplete && accumulator.MailState == AccumulatedMailState.Ready;
    public bool IsTestActionVisible => !IsAccumulating && IsTestModeEnabled && IsConfigurationComplete;
    public bool IsTestActionEnabled => AreControlsEnabled && IsTestActionVisible;
    public bool IsStartAutomationWarningVisible => AreControlsEnabled && !IsConfigurationComplete;
    public string StartAutomationWarningMessage => GetStartAutomationWarningMessage();
    public string ConfigurationSummaryText => IsConfigurationComplete ? "Configuración completa" : "Configuración pendiente";
    public string CredentialsActionText => AreCredentialsConfigured ? "Editar credenciales" : "Configurar credenciales";
    public string RequestActionText => favoriteCollectionsState.ActiveCollection is null
        ? "Selecciona una colección"
        : IsRequestConfigured ? "Editar solicitud" : "Configurar solicitud";
    public string SignatureActionText => "Configurar firma del correo";
    public string ActiveCollectionText => favoriteCollectionsState.ActiveCollection is FavoriteCollection collection
        ? $"Colección activa: {collection.Name}"
        : "Selecciona una colección para automatizar";

    /// <summary>Places the main window in its safe loading state before it becomes visible.</summary>
    public void BeginInitialization()
    {
        IsInitializing = true;
        StatusMessage = "Preparando PAM Automation…";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (settingsStore is null || credentialStore is null)
        {
            IsInitializing = false;
            return;
        }

        logger.LogInformation("Main window configuration initialization started.");
        try
        {
            settings = await settingsStore.LoadAsync(cancellationToken);
            bool requestStartAdjusted = TryMoveExpiredRequestStartToCurrentMinimum(
                settings,
                GetEcuadorNow(),
                out AppSettings adjustedSettings);
            if (requestStartAdjusted)
            {
                settings = adjustedSettings;
                await SaveSettingsAsync(settings);
                logger.LogInformation("Expired request start was advanced during application initialization.");
            }
            AreCredentialsConfigured = credentialStore.HasCredentials();
            await RefreshInteractivePamAccountAvailabilityAsync(cancellationToken);
            IsTestModeEnabled = settings.TestModeEnabled;
            SendEmailAtEnd = settings.FinalAction == FinalAction.SendEmail;
            await ReloadFavoriteCollectionsAsync(cancellationToken);
            IsRequestConfigured = IsEffectiveRequestConfigured;
            OutlookAccountSettings? outlookAccount = outlookAccountSettingsStore is null
                ? null
                : await outlookAccountSettingsStore.LoadAsync(cancellationToken);
            MailSettings? legacyMail = outlookAccount is null && mailSettingsStore is not null
                ? await mailSettingsStore.LoadAsync(cancellationToken) : null;
            if (outlookAccount is null && legacyMail is not null && outlookAccountSettingsStore is not null)
            {
                outlookAccount = new OutlookAccountSettings { SmtpAddress = legacyMail.SmtpAddress };
                await outlookAccountSettingsStore.SaveAsync(outlookAccount, cancellationToken);
            }
            outlookSmtpAddress = outlookAccount?.SmtpAddress ?? legacyMail?.SmtpAddress ?? string.Empty;
            try
            {
                mailSignature = mailSignatureStore is null ? null : await mailSignatureStore.LoadAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                signatureLoadError = "No se pudo leer la firma. Abre Configurar firma del correo para volver a guardarla.";
                logger.LogWarning(exception, "The protected mail signature could not be loaded.");
            }
            RefreshEffectiveMail();
            StatusMessage = requestStartAdjusted
                ? "La fecha y hora vencidas se actualizaron automáticamente a la primera hora válida."
                : IsConfigurationComplete
                    ? "La configuración está lista para ejecutar."
                    : "Completa o actualiza la configuración para habilitar la automatización.";
            RefreshPresentationState();
            logger.LogInformation("Main window configuration initialization completed. Complete={IsComplete}.", IsConfigurationComplete);
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>Checks for a newer portable release without blocking or failing application startup.</summary>
    /// <param name="cancellationToken">Token cancelled when the application closes.</param>
    /// <returns>A task that completes after the optional remote check.</returns>
    public async Task CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (updateService is null) return;

        try
        {
            var result = await updateService.CheckAsync(applicationVersion, cancellationToken);
            if (!result.IsUpdateAvailable) return;

            UpdateNotificationMessage =
                $"Hay una nueva versión de PAM Automation disponible en el repositorio compartido: " +
                $"V. {result.LatestVersion.ToString(3)}.";
            IsUpdateAvailable = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Application update check was cancelled during shutdown.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Application update check failed safely.");
        }
    }

    partial void OnAreCredentialsConfiguredChanged(bool value) => RefreshPresentationState();
    partial void OnIsInitializingChanged(bool value) => RefreshPresentationState();
    partial void OnIsRequestConfiguredChanged(bool value) => RefreshPresentationState();
    partial void OnIsMailConfiguredChanged(bool value) => RefreshPresentationState();
    partial void OnIsTestModeEnabledChanged(bool value) => RefreshPresentationState();
    partial void OnSendEmailAtEndChanged(bool value)
    {
        settings = settings with
        {
            FinalAction = value ? FinalAction.SendEmail : FinalAction.DisplayRequestIds
        };
        RefreshPresentationState();
    }
    partial void OnIsRunningChanged(bool value) => RefreshPresentationState();
    partial void OnIsExportingDiagnosticsChanged(bool value) => RefreshPresentationState();
    partial void OnIsAccessingPamChanged(bool value) => RefreshPresentationState();
    partial void OnHasResultChanged(bool value) => RefreshPresentationState();

    private async Task AccessPamAsync()
    {
        if (!CanAccessPam) return;
        bool preserveAutomationStatus = IsRunning;

        PamCredentials? credentials;
        try
        {
            credentials = await SelectInteractiveCredentialsAsync();
        }
        catch (Exception exception)
        {
            if (preserveAutomationStatus)
            {
                PresentConcurrentAccessError(exception);
            }
            else
            {
                PresentError(exception);
            }
            return;
        }

        if (credentials is null) return;

        IsAccessingPam = true;
        if (!preserveAutomationStatus)
        {
            StatusMessage = "Abriendo BeyondInsight e iniciando sesión…";
        }
        logger.LogInformation("Interactive PAM access started with the confirmed identity.");
        try
        {
            await interactiveAccessService!.OpenAsync(credentials).ConfigureAwait(true);
            if (!preserveAutomationStatus)
            {
                StatusMessage = "BeyondInsight está abierto y la sesión PAM se inició correctamente.";
            }
            logger.LogInformation("Interactive PAM access completed successfully.");
        }
        catch (Exception exception)
        {
            if (preserveAutomationStatus)
            {
                PresentConcurrentAccessError(exception);
            }
            else
            {
                PresentError(exception);
            }
        }
        finally
        {
            IsAccessingPam = false;
        }
    }

    private async Task<PamCredentials?> SelectInteractiveCredentialsAsync()
    {
        if (pamAccountService is null)
        {
            PamCredentials? legacyCredentials = credentialStore?.LoadForAuthentication();
            if (legacyCredentials is null) PresentMissingInteractiveCredentials();
            return legacyCredentials;
        }

        IReadOnlyList<PamAccountStatus> accounts = await pamAccountService.GetAccountsAsync();
        hasInteractivePamAccount = accounts.Any(status => status.HasCredentials);
        RefreshPresentationState();
        if (!hasInteractivePamAccount)
        {
            PresentMissingInteractiveCredentials();
            return null;
        }

        PamAccountId? selectedId = configurationDialogs!.SelectPamAccount(accounts);
        if (selectedId is null)
        {
            logger.LogInformation("Interactive PAM account selection was cancelled.");
            return null;
        }

        PamAccountStatus? selected = accounts.FirstOrDefault(status => status.Account.Id == selectedId.Value);
        if (selected is null || !selected.HasCredentials)
        {
            await RefreshInteractivePamAccountAvailabilityAsync();
            PresentMissingInteractiveCredentials();
            return null;
        }

        PamCredentials? credentials = await pamAccountService.LoadCredentialsAsync(selectedId.Value);
        if (credentials is null)
        {
            await RefreshInteractivePamAccountAvailabilityAsync();
            PresentMissingInteractiveCredentials();
            return null;
        }

        logger.LogInformation(
            "Interactive PAM identity confirmed. AccountId={AccountId}; IsPrimary={IsPrimary}.",
            selected.Account.Id,
            selected.Account.IsPrimary);
        return credentials;
    }

    private void PresentMissingInteractiveCredentials()
    {
        const string message = "El usuario PAM seleccionado no tiene credenciales completas. Abre Usuarios PAM para configurarlas.";
        if (IsRunning) dialogs?.ShowError(message);
        else StatusMessage = message;
    }

    private async Task StartAutomationAsync()
    {
        if (!AreControlsEnabled || accumulator.MailState != AccumulatedMailState.Ready) return;
        RefreshEffectiveMail();
        if (!IsAccumulating && SendEmailAtEnd && !IsMailConfigured)
        {
            StatusMessage = mailConfigurationError;
            dialogs?.ShowError(StatusMessage);
            return;
        }
        MailSettings? executionMail = !IsAccumulating && SendEmailAtEnd ? mailSettings : null;
        AppSettings executionSettings = GetEffectiveSettings();
        if (IsAccumulating) executionSettings = executionSettings with { FinalAction = FinalAction.DisplayRequestIds };
        if (!ValidateRequestAtExecutionBoundary(executionSettings, confirmationWasShown: false)) return;
        if (!TryGetExecutionDependencies(out PamCredentials? credentials)) return;

        FavoriteCollection? activeCollection = favoriteCollectionsState.ActiveCollection;
        if (activeCollection is null || activeCollection.Accounts.Count == 0)
        {
            StatusMessage = activeCollection is null
                ? "Selecciona una colección antes de iniciar la automatización."
                : "La colección activa no contiene accesos disponibles. Actualiza Favorites o sus accesos antes de iniciar otra ejecución.";
            dialogs!.ShowError(StatusMessage);
            return;
        }

        if (accumulator.CollectionId is Guid lockedId && activeCollection.Id != lockedId)
        {
            dialogs!.ShowError(RequestIdAccumulator.CollectionLockMessage);
            return;
        }
        if (!dialogs!.ConfirmAutomationStart(executionSettings, executionMail, activeCollection.Name, IsAccumulating))
        {
            logger.LogInformation("Real PAM execution was cancelled before preparation.");
            StatusMessage = "Ejecución cancelada. No se inició ninguna validación ni se creó ninguna solicitud.";
            return;
        }

        if (!ValidateRequestAtExecutionBoundary(executionSettings, confirmationWasShown: true)) return;

        IsRunning = true;
        logger.LogInformation("Real PAM execution started.");
        HasResult = false;
        StatusMessage = "Validando Favorites y Pending en segundo plano…";
        try
        {
            PamExecutionPreparation preparation = await executionService!.PrepareCollectionRealAsync(
                credentials!,
                executionSettings,
                activeCollection.Accounts,
                GetEcuadorNow());
            if (preparation.HasExactPendingDuplicates)
            {
                if (IsAccumulating)
                {
                    StatusMessage = "Ya existen solicitudes idénticas en Pending. Cambia inicio, duración o motivo. No se incorporaron IDs antiguos ni se envió correo.";
                    dialogs!.ShowError(StatusMessage);
                    return;
                }
                RepeatedPendingDecision decision = dialogs!.ChooseRepeatedPendingAction(preparation);
                if (decision != RepeatedPendingDecision.UseExistingRequestIds)
                {
                    logger.LogInformation(
                        "Real PAM execution was cancelled because exact Pending duplicates were found. DuplicateCount={DuplicateCount}.",
                        preparation.ExactPendingDuplicateCount);
                    StatusMessage = preparation.CanReusePendingRequestIdsForEmail
                        ? "Ejecución cancelada. No se crearon solicitudes repetidas ni se envió correo."
                        : "Ejecución cancelada. Cambia la fecha u hora de inicio, la duración o el motivo antes de volver a intentarlo.";
                    return;
                }

                if (executionMail is null)
                {
                    throw new InvalidOperationException("La configuración de correo no está disponible.");
                }

                StatusMessage = "Enviando correo con los Request ID existentes en Pending…";
                PamExecutionResult existingResult = await executionService
                    .CompleteWithExistingPendingAsync(preparation, executionMail)
                    .ConfigureAwait(true);
                PresentResult(existingResult);
                PresentExistingPendingMailOutcome(existingResult.MailDelivery);
                return;
            }

            var progress = new Progress<PamRequestSubmissionProgress>(PresentSubmissionProgress);
            PamExecutionResult result = await executionService.ExecuteRealAsync(
                credentials!, preparation, GetEcuadorNow(), executionMail, progress: progress);
            if (IsAccumulating)
            {
                accumulator.Add(activeCollection.Id, result);
                lastExecutionResult = result;
                HasResult = false;
                HasRequestIds = false;
                RequestIdsText = string.Empty;
                StatusMessage = $"Ejecución completada. {AccumulatedCountText}. No se envió correo.";
            }
            else PresentResult(result);
        }
        catch (Exception exception)
        {
            if (IsAccumulating)
            {
                logger.LogError(exception, "Accumulating PAM execution failed; previous IDs retained.");
                HasResult = false;
                StatusMessage = "La ejecución no aportó IDs al acumulado. Se conservan los anteriores. Pueden existir solicitudes creadas en Pending; revísalas antes de reintentar.";
                dialogs?.ShowError(GetSafeErrorMessage(exception) + " " + StatusMessage);
            }
            else PresentError(exception);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ConfigureCredentialsAsync()
    {
        if (credentialStore is null || configurationDialogs is null)
        {
            ShowPendingFeature("La configuración de credenciales no está disponible en este contexto.");
            return;
        }

        try
        {
            logger.LogInformation("Credential configuration dialog opened.");
            var currentState = new CredentialConfigurationState(
                AreCredentialsConfigured,
                !string.IsNullOrWhiteSpace(outlookSmtpAddress),
                outlookSmtpAddress);
            CredentialConfigurationState state = await configurationDialogs.EditCredentialsAsync(currentState);
            AreCredentialsConfigured = state.HasPamCredentials;
            await RefreshInteractivePamAccountAvailabilityAsync();
            outlookSmtpAddress = state.OutlookSmtpAddress;
            RefreshEffectiveMail();
            StatusMessage = state == currentState
                ? "Configuración de credenciales sin cambios."
                : "Configuración de credenciales actualizada.";
            logger.LogInformation("Credential configuration dialog completed. ConfigurationChanged={ConfigurationChanged}.", state != currentState);
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
    }

    private async Task ConfigureRequestAsync()
    {
        if (settingsStore is null || configurationDialogs is null)
        {
            ShowPendingFeature("La configuración de solicitud no está disponible en este contexto.");
            return;
        }

        FavoriteCollection? activeCollection = favoriteCollectionsState.ActiveCollection;
        if (activeCollection is null)
        {
            StatusMessage = "Selecciona una colección antes de configurar la fecha, hora y duración.";
            dialogs?.ShowError(StatusMessage);
            return;
        }

        RequestConfigurationEdit? edit = configurationDialogs.EditRequest(
            settings.Request with { RequestReason = activeCollection.RequestReason },
            settings.TestModeEnabled,
            DateOnly.FromDateTime(GetEcuadorNow().DateTime));
        if (edit is null)
        {
            logger.LogInformation("Request configuration dialog was cancelled.");
            return;
        }

        AppSettings proposed = settings with
        {
            Request = edit.Request with { RequestReason = settings.Request.RequestReason },
            TestModeEnabled = edit.TestModeEnabled,
            FinalAction = SendEmailAtEnd ? FinalAction.SendEmail : FinalAction.DisplayRequestIds
        };
        AppSettings effectiveProposed = proposed with
        {
            Request = proposed.Request with { RequestReason = activeCollection.RequestReason }
        };
        ConfigurationValidationResult validation = settingsValidator.Validate(effectiveProposed, GetEcuadorNow());
        if (!validation.IsValid)
        {
            logger.LogWarning("Request configuration validation failed. FailureCount={FailureCount}.", validation.Failures.Count);
            StatusMessage = validation.Failures[0].Message;
            dialogs?.ShowError(StatusMessage);
            return;
        }

        try
        {
            await SaveSettingsAsync(proposed);
            settings = proposed;
            IsRequestConfigured = true;
            IsTestModeEnabled = proposed.TestModeEnabled;
            StatusMessage = "Configuración de solicitud guardada.";
            logger.LogInformation("Request configuration was stored successfully.");
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
    }

    private async Task OpenCollectionsAsync()
    {
        if (configurationDialogs is null)
        {
            ShowPendingFeature("La sección de colecciones no está disponible en este contexto.");
            return;
        }

        try
        {
            logger.LogInformation("Collections dialog opened.");
            configurationDialogs.OpenFavoriteCollections(outlookSmtpAddress);
            await ReloadFavoriteCollectionsAsync();
            logger.LogInformation("Collections dialog closed.");
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
    }

    private async Task OpenPamAccountsAsync()
    {
        try
        {
            configurationDialogs?.OpenPamAccounts();
            await RefreshInteractivePamAccountAvailabilityAsync();
            StatusMessage = "Administración de usuarios PAM cerrada.";
        }
        catch (Exception exception) { PresentError(exception); }
    }

    private void OpenAbout()
    {
        try
        {
            configurationDialogs?.OpenAbout();
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
    }

    private async Task RefreshInteractivePamAccountAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (pamAccountService is null)
        {
            hasInteractivePamAccount = AreCredentialsConfigured;
            PrincipalAccountName = string.Empty;
            PrincipalAccountInitial = "?";
            RefreshPresentationState();
            return;
        }

        IReadOnlyList<PamAccountStatus> accounts = await pamAccountService.GetAccountsAsync(cancellationToken);
        PamAccountSummary? principal = accounts.FirstOrDefault(status => status.Account.IsPrimary)?.Account;
        hasInteractivePamAccount = accounts.Any(status => status.HasCredentials);
        PrincipalAccountName = principal?.Name ?? string.Empty;
        PrincipalAccountInitial = GetAccountInitial(principal?.Name);
        RefreshPresentationState();
    }

    private static string GetAccountInitial(string? name)
    {
        string? firstTextElement = string.IsNullOrWhiteSpace(name)
            ? null
            : System.Globalization.StringInfo.GetNextTextElement(name.Trim());
        return string.IsNullOrWhiteSpace(firstTextElement)
            ? "?"
            : firstTextElement.ToUpperInvariant();
    }

    private async Task StartTestAsync()
    {
        if (IsAccumulating) return;
        RefreshEffectiveMail();
        if (SendEmailAtEnd && !IsMailConfigured)
        {
            dialogs?.ShowError(mailConfigurationError);
            return;
        }
        if (!TryGetExecutionDependencies(out PamCredentials? credentials)) return;

        IsRunning = true;
        logger.LogInformation("Non-destructive PAM test execution started.");
        HasResult = false;
        StatusMessage = "Validando y descartando formularios; Submit Request permanece bloqueado…";
        try
        {
            PamExecutionResult result = await executionService!.ExecuteTestAsync(
                credentials!, settings, GetEcuadorNow());
            if (SendEmailAtEnd)
            {
                if (mailSettings is null) throw new InvalidOperationException("La configuración de correo no está disponible.");
                if (!dialogs!.ConfirmTestEmail("tellezoe@hitss.com"))
                {
                    result = new PamExecutionResult(
                        result.Mode, result.CompletedAt, result.FinalAction, result.FavoriteCount,
                        result.ProcessedCount, result.ConflictCount, result.RequestIds,
                        OutlookDeliveryResult.SkippedByUser());
                }
                else
                {
                    OutlookDeliveryResult delivery = await executionService.SendResultEmailAsync(
                        mailSettings, PamRequestTestResultIds.Values, isTest: true);
                    result = new PamExecutionResult(
                        result.Mode, result.CompletedAt, result.FinalAction, result.FavoriteCount,
                        result.ProcessedCount, result.ConflictCount, result.RequestIds, delivery);
                }
            }
            PresentResult(result);
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ConfigureSignatureAsync()
    {
        if (configurationDialogs is null || mailSignatureStore is null)
        {
            ShowPendingFeature("La configuración de firma no está disponible en este contexto.");
            return;
        }

        MailSignature? edit = configurationDialogs.EditSignature(mailSignature);
        if (edit is null)
        {
            logger.LogInformation("Mail signature dialog was cancelled.");
            return;
        }
        IsRunning = true;
        StatusMessage = "Validando y guardando la firma común…";
        try
        {
            MailSignature normalized = CollectionMailComposer.ValidateSignature(edit);
            await mailSignatureStore.SaveAsync(normalized);
            mailSignature = normalized;
            signatureLoadError = string.Empty;
            RefreshEffectiveMail();
            StatusMessage = "Firma guardada de forma protegida. Se usará en los próximos correos de todas las colecciones.";
            logger.LogInformation("The principal mail signature was stored successfully.");
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool TryGetExecutionDependencies(out PamCredentials? credentials)
    {
        credentials = null;
        if (executionService is null || credentialStore is null || dialogs is null)
        {
            StatusMessage = "El flujo de ejecución no está disponible en este contexto.";
            return false;
        }

        credentials = credentialStore.LoadForAuthentication();
        if (credentials is not null) return true;

        AreCredentialsConfigured = false;
        StatusMessage = "No hay credenciales PAM configuradas.";
        return false;
    }

    internal void RefreshTimeSensitiveState()
    {
        bool wasRequestConfigured = IsRequestConfigured;
        ConfigurationValidationResult validation = settingsValidator.Validate(GetEffectiveSettings(), GetEcuadorNow());
        IsRequestConfigured = favoriteCollectionsState.ActiveCollection is not null && validation.IsValid;
        RefreshPresentationState();

        if (wasRequestConfigured && !validation.IsValid && !IsRunning)
        {
            StatusMessage = validation.Failures.Any(failure =>
                failure.Code == ConfigurationValidationErrorCode.StartTimeTooClose)
                ? "La solicitud quedó demasiado cerca de la hora actual. Configura un inicio con al menos 6 minutos de anticipación."
                : "La fecha u hora de inicio configurada ya pasó. Abre Editar solicitud para actualizarla.";
            logger.LogInformation("The configured request start time no longer satisfies the required lead time while the application remained open.");
        }
    }

    private bool ValidateRequestAtExecutionBoundary(AppSettings executionSettings, bool confirmationWasShown)
    {
        ConfigurationValidationResult validation = settingsValidator.Validate(executionSettings, GetEcuadorNow());
        IsRequestConfigured = favoriteCollectionsState.ActiveCollection is not null && validation.IsValid;
        RefreshPresentationState();
        if (validation.IsValid) return true;

        ConfigurationValidationFailure? timeFailure = validation.Failures.FirstOrDefault(failure =>
            failure.Code is ConfigurationValidationErrorCode.StartDateInPast or
                ConfigurationValidationErrorCode.StartTimeInPast or
                ConfigurationValidationErrorCode.StartTimeTooClose);
        bool isTooClose = timeFailure?.Code == ConfigurationValidationErrorCode.StartTimeTooClose;
        string message = isTooClose && confirmationWasShown
            ? "La hora de inicio quedó a menos de 6 minutos mientras la confirmación estaba abierta. La automatización se canceló antes de contactar PAM o crear solicitudes. Configura una hora con al menos 6 minutos de anticipación."
            : isTooClose
                ? "La hora de inicio está demasiado cerca. Debe estar al menos 6 minutos después de la hora actual de Ecuador. La automatización no comenzó."
            : timeFailure is not null && confirmationWasShown
            ? "La hora de inicio venció mientras la confirmación estaba abierta. La automatización se canceló antes de contactar PAM o crear solicitudes. Abre Configurar solicitud y elige una nueva hora."
            : timeFailure is not null
                ? "La fecha u hora de inicio configurada ya pasó. La automatización no comenzó. Abre Configurar solicitud y elige una nueva hora."
                : validation.Failures[0].Message;
        StatusMessage = message;
        dialogs?.ShowError(message);
        logger.LogInformation(
            "Real PAM execution was blocked by a fresh local settings validation. ConfirmationWasShown={ConfirmationWasShown}, FailureCode={FailureCode}.",
            confirmationWasShown,
            validation.Failures[0].Code);
        return false;
    }

    private void PresentResult(PamExecutionResult result)
    {
        logger.LogInformation(
            "PAM execution completed. Mode={Mode}, Favorites={FavoriteCount}, Processed={ProcessedCount}, Conflicts={ConflictCount}, RequestIdCount={RequestIdCount}.",
            result.Mode, result.FavoriteCount, result.ProcessedCount, result.ConflictCount, result.RequestIds.Count);
        lastExecutionResult = result;
        HasResult = true;
        HasRequestIds = result.RequestIds.Count > 0;
        ResultTitle = result.Mode switch
        {
            PamExecutionMode.Test => "Prueba completada",
            PamExecutionMode.ExistingPending => "Solicitudes existentes utilizadas",
            _ => "Automatización completada",
        };
        ResultSummary = result.Mode switch
        {
            PamExecutionMode.Test =>
                $"{result.ProcessedCount} de {result.FavoriteCount} formularios fueron validados y descartados. Solicitudes creadas: 0. Finalizó: {result.CompletedAt:dd/MM/yyyy hh:mm tt}.",
            PamExecutionMode.ExistingPending =>
                $"Se utilizaron {result.RequestIds.Count} Request ID existentes en Pending. Solicitudes nuevas creadas: 0. Finalizó: {result.CompletedAt:dd/MM/yyyy hh:mm tt}.",
            _ =>
                $"Procesadas: {result.ProcessedCount} de {result.FavoriteCount}. Request ID: {result.RequestIds.Count}. Conflictos: {result.ConflictCount}. Finalizó: {result.CompletedAt:dd/MM/yyyy hh:mm tt}.",
        };
        if (result.MailDelivery is not null) ResultSummary += " " + result.MailDelivery.Message;
        RequestIdsText = string.Join(Environment.NewLine, result.RequestIds);
        StatusMessage = ResultTitle;
    }

    private void ShowResult()
    {
        if (!CanShowResult) return;

        string message = HasRequestIds
            ? ResultSummary + Environment.NewLine + Environment.NewLine + "Request ID:" +
              Environment.NewLine + RequestIdsText
            : ResultSummary;
        dialogs!.ShowResult(ResultTitle, message, HasRequestIds ? RequestIdsText : null);
    }

    private void PresentExistingPendingMailOutcome(OutlookDeliveryResult? delivery)
    {
        if (delivery is null) return;

        switch (delivery.Status)
        {
            case OutlookDeliveryStatus.SentConfirmed:
                dialogs!.ShowResult(
                    "Correo enviado",
                    "El correo con los Request ID existentes fue enviado correctamente y localizado en Enviados.");
                break;
            case OutlookDeliveryStatus.SubmittedUnconfirmed:
                dialogs!.ShowResult(
                    "Envío aceptado por Outlook",
                    "Outlook recibió correctamente la solicitud para enviar el correo con los Request ID existentes.");
                break;
            case OutlookDeliveryStatus.OutboxPending:
                dialogs!.ShowResult(
                    "Correo pendiente en Outlook",
                    "Outlook recibió el correo, pero todavía permanece en Bandeja de salida. Outlook continuará procesándolo.");
                break;
            default:
                dialogs!.ShowError(delivery.Message);
                break;
        }
    }

    private void PresentError(Exception exception)
    {
        logger.LogError(exception, "Application operation failed.");
        string message = GetSafeErrorMessage(exception);
        if (exception is PamRequestSubmissionException { ProcessedCount: > 0 } partial)
        {
            HasResult = true;
            HasRequestIds = false;
            ResultTitle = "Automatización incompleta";
            ResultSummary = $"{partial.ProcessedCount} Favorites rows alcanzaron un resultado conocido antes del error. Revisa Pending antes de reintentar.";
            RequestIdsText = string.Empty;
        }
        else
        {
            HasResult = false;
        }
        StatusMessage = diagnosticExportService is null
            ? message
            : message + " Si el problema continúa, usa Exportar diagnóstico y envía el archivo a soporte.";
        dialogs?.ShowError(StatusMessage);
    }

    private void PresentConcurrentAccessError(Exception exception)
    {
        logger.LogError(exception, "Concurrent interactive PAM access failed without altering the running execution state.");
        string message = GetSafeErrorMessage(exception);
        if (diagnosticExportService is not null)
        {
            message += " Si el problema continúa, usa Exportar diagnóstico y envía el archivo a soporte.";
        }
        dialogs?.ShowError(message);
    }

    internal void PresentSubmissionProgress(PamRequestSubmissionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Status != PamRequestSubmissionStatus.Submitted) return;

        string applicationName = progress.Account.ApplicationName
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        StatusMessage = $"Se solicitó {applicationName}.";
    }

    private static string GetSafeErrorMessage(Exception exception) => exception switch
    {
        PamAuthenticationException or PamFavoritesException or FavoriteCollectionException or
            PamRequestSubmissionException or
            PamPendingException or PamRequestTestException or OutlookValidationException or
            OutlookOperationException or OutlookAccountResolutionException => exception.Message,
        OperationCanceledException => "La operación fue cancelada.",
        _ => "Ocurrió un error no esperado. No se muestran detalles que puedan contener información sensible."
    };

    private async Task ExportDiagnosticsAsync()
    {
        if (diagnosticExportService is null || dialogs is null) return;
        string suggestedName = $"pam-automation-diagnostic-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.zip";
        string? destinationPath = dialogs.SelectDiagnosticExportPath(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            logger.LogInformation("Diagnostic export path selection was cancelled.");
            return;
        }

        IsExportingDiagnostics = true;
        StatusMessage = "Preparando el diagnóstico seguro…";
        logger.LogInformation("Diagnostic export started after explicit user selection.");
        try
        {
            await diagnosticExportService.ExportAsync(
                destinationPath,
                CaptureDiagnosticContext()).ConfigureAwait(true);
            logger.LogInformation("Diagnostic export completed successfully.");
            StatusMessage = "Diagnóstico exportado correctamente.";
            dialogs.ShowResult(
                "Diagnóstico exportado",
                "El paquete se guardó correctamente. Puedes enviarlo a soporte para analizar el problema.");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Diagnostic export was cancelled.");
            StatusMessage = "La exportación del diagnóstico fue cancelada.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Diagnostic export failed.");
            StatusMessage = "No fue posible exportar el diagnóstico. No se dejó un archivo incompleto.";
            dialogs.ShowError(StatusMessage);
        }
        finally
        {
            IsExportingDiagnostics = false;
        }
    }

    internal DiagnosticExportContext CaptureDiagnosticContext()
    {
        DiagnosticExecutionSummary? execution = lastExecutionResult is null
            ? null
            : new DiagnosticExecutionSummary(
                lastExecutionResult.Mode.ToString(),
                lastExecutionResult.CompletedAt,
                lastExecutionResult.FinalAction.ToString(),
                lastExecutionResult.FavoriteCount,
                lastExecutionResult.ProcessedCount,
                lastExecutionResult.ConflictCount,
                lastExecutionResult.RequestIds.Count,
                lastExecutionResult.MailDelivery?.Status.ToString());
        return new DiagnosticExportContext(
            AreCredentialsConfigured,
            IsRequestConfigured,
            IsMailConfigured,
            execution);
    }

    private DateTimeOffset GetEcuadorNow() => timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(-5));

    private async Task ToggleFinalActionAsync()
    {
        if (!CanChangeFinalAction) return;
        SendEmailAtEnd = !SendEmailAtEnd;
        if (settingsStore is null) return;
        try
        {
            await SaveSettingsAsync(settings);
            StatusMessage = SendEmailAtEnd
                ? "Acción final cambiada a envío por correo."
                : "Acción final cambiada a mostrar Request ID.";
            logger.LogInformation("Final action configuration changed. FinalAction={FinalAction}.", settings.FinalAction);
        }
        catch (Exception exception)
        {
            PresentError(exception);
        }
    }

    private async Task ReloadFavoriteCollectionsAsync(CancellationToken cancellationToken = default)
    {
        if (favoriteCollectionService is null) return;
        FavoriteCollectionsState state = await favoriteCollectionService.LoadAsync(cancellationToken);
        ApplyFavoriteCollectionsState(state);
    }

    private void ApplyFavoriteCollectionsState(FavoriteCollectionsState state)
    {
        favoriteCollectionsState = state;
        RefreshEffectiveMail();
        IsRequestConfigured = state.ActiveCollection is not null &&
                              settingsValidator.Validate(GetEffectiveSettings(), GetEcuadorNow()).IsValid;
        RefreshPresentationState();
    }

    private void RefreshEffectiveMail()
    {
        mailSettings = null;
        FavoriteCollection? collection = favoriteCollectionsState.ActiveCollection;
        mailConfigurationError = collection is null
            ? "Selecciona una colección y configura su correo."
            : $"Configura el correo de la colección «{collection.Name}» en Colecciones → Configurar correo de esta colección: Para, CC opcional, asunto y contenido con un marcador de Request ID.";
        if (collection is not null)
        {
            try
            {
                if (!string.IsNullOrEmpty(signatureLoadError)) throw new InvalidOperationException(signatureLoadError);
                mailSettings = CollectionMailComposer.Resolve(collection, outlookSmtpAddress, mailSignature);
                mailConfigurationError = string.Empty;
            }
            catch (Exception exception) when (exception is OutlookValidationException or OutlookHtmlCompositionException or InvalidOperationException or System.IO.InvalidDataException)
            {
                mailConfigurationError += " " + exception.Message;
            }
        }
        IsMailConfigured = mailSettings is not null;
        RefreshPresentationState();
    }

    private AppSettings GetEffectiveSettings() =>
        favoriteCollectionsState.ActiveCollection is FavoriteCollection collection
            ? settings with
            {
                Request = settings.Request with { RequestReason = collection.RequestReason }
            }
            : settings;

    private static bool TryMoveExpiredRequestStartToCurrentMinimum(
        AppSettings current,
        DateTimeOffset ecuadorNow,
        out AppSettings adjusted)
    {
        DateTime configuredStart = current.Request.StartDate.ToDateTime(current.Request.StartTime);
        if (configuredStart >= ecuadorNow.DateTime)
        {
            adjusted = current;
            return false;
        }

        DateTime earliestValid = ecuadorNow.DateTime.Add(RequestSettingsValidator.MinimumLeadTime);
        DateTime roundedToMinute = new(
            earliestValid.Year,
            earliestValid.Month,
            earliestValid.Day,
            earliestValid.Hour,
            earliestValid.Minute,
            0,
            earliestValid.Kind);
        if (earliestValid > roundedToMinute) roundedToMinute = roundedToMinute.AddMinutes(1);

        adjusted = current with
        {
            Request = current.Request with
            {
                StartDate = DateOnly.FromDateTime(roundedToMinute),
                StartTime = TimeOnly.FromDateTime(roundedToMinute)
            }
        };
        return true;
    }

    private async Task SaveSettingsAsync(AppSettings value)
    {
        if (settingsStore is null) return;
        await settingsSaveGate.WaitAsync();
        try
        {
            await settingsStore.SaveAsync(value);
        }
        finally
        {
            settingsSaveGate.Release();
        }
    }

    private void ShowPendingFeature(string message) => StatusMessage = message;

    private string GetStartAutomationWarningMessage()
    {
        if (!AreCredentialsConfigured)
        {
            return "El botón está desactivado porque faltan las credenciales PAM. Abre Editar credenciales para configurarlas.";
        }

        if (favoriteCollectionsState.ActiveCollection is null)
        {
            return "El botón está desactivado porque no hay una colección activa. Abre Colecciones y selecciona una.";
        }

        if (favoriteCollectionsState.ActiveCollection.Accounts.Count == 0)
            return AccumulatedCount > 0
                ? "La colección activa ya no tiene accesos disponibles. El acumulado se conserva y no se cambiará de colección; actualiza sus accesos o envía/descarta el acumulado."
                : "La colección activa no contiene accesos disponibles. Actualiza Favorites o selecciona una colección con accesos.";

        if (!IsEffectiveRequestConfigured)
        {
            ConfigurationValidationFailure? failure = settingsValidator
                .Validate(GetEffectiveSettings(), GetEcuadorNow())
                .Failures
                .FirstOrDefault();

            return failure?.Code switch
            {
                ConfigurationValidationErrorCode.StartDateInPast =>
                    "El botón está desactivado porque la fecha de inicio configurada ya pasó. Abre Editar solicitud y elige una fecha y hora futuras.",
                ConfigurationValidationErrorCode.StartTimeInPast =>
                    "El botón está desactivado porque la hora de inicio configurada ya pasó. Abre Editar solicitud y elige una fecha y hora futuras.",
                ConfigurationValidationErrorCode.StartTimeTooClose =>
                    "El botón está desactivado porque la hora de inicio está demasiado cerca. Configura una hora que esté al menos 6 minutos después de la hora actual de Ecuador.",
                _ when failure is not null =>
                    $"El botón está desactivado porque la solicitud no es válida: {failure.Message} Abre Configurar solicitud para corregirla.",
                _ =>
                    "El botón está desactivado porque falta configurar la solicitud. Abre Configurar solicitud para completarla."
            };
        }

        if (!IsAccumulating && SendEmailAtEnd && !IsMailConfigured)
        {
            return "El botón está desactivado porque elegiste Outlook. " + mailConfigurationError;
        }

        return string.Empty;
    }

    private void RefreshPresentationState()
    {
        RefreshAccumulationPresentation();
        OnPropertyChanged(nameof(AreControlsEnabled));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanExportDiagnostics));
        OnPropertyChanged(nameof(CanAccessPam));
        OnPropertyChanged(nameof(CanShowResult));
        OnPropertyChanged(nameof(IsConfigurationComplete));
        OnPropertyChanged(nameof(IsEffectiveRequestConfigured));
        OnPropertyChanged(nameof(CanStartAutomation));
        OnPropertyChanged(nameof(IsTestActionVisible));
        OnPropertyChanged(nameof(IsTestActionEnabled));
        OnPropertyChanged(nameof(IsStartAutomationWarningVisible));
        OnPropertyChanged(nameof(StartAutomationWarningMessage));
        OnPropertyChanged(nameof(ConfigurationSummaryText));
        OnPropertyChanged(nameof(CredentialsActionText));
        OnPropertyChanged(nameof(RequestActionText));
        OnPropertyChanged(nameof(SignatureActionText));
        OnPropertyChanged(nameof(ActiveCollectionText));
        ConfigureCredentialsCommand.NotifyCanExecuteChanged();
        ConfigureRequestCommand.NotifyCanExecuteChanged();
        ConfigureSignatureCommand.NotifyCanExecuteChanged();
        OpenCollectionsCommand.NotifyCanExecuteChanged();
        OpenPamAccountsCommand.NotifyCanExecuteChanged();
        OpenAboutCommand.NotifyCanExecuteChanged();
        StartTestCommand.NotifyCanExecuteChanged();
        StartAutomationCommand.NotifyCanExecuteChanged();
        ToggleFinalActionCommand.NotifyCanExecuteChanged();
        ExportDiagnosticsCommand.NotifyCanExecuteChanged();
        AccessPamCommand.NotifyCanExecuteChanged();
        ShowResultCommand.NotifyCanExecuteChanged();
    }

}
