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
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Interfaces.Updates;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
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
    private readonly IOutlookAccountSettingsStore? outlookAccountSettingsStore;
    private readonly MailSettingsValidator mailSettingsValidator;
    private readonly AppSettingsValidator settingsValidator;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IDiagnosticExportService? diagnosticExportService;
    private readonly IApplicationUpdateService? updateService;
    private readonly IPamInteractiveAccessService? interactiveAccessService;
    private readonly Version applicationVersion;
    private AppSettings settings = new();
    private MailSettings? mailSettings;
    private string outlookSmtpAddress = string.Empty;
    private readonly SemaphoreSlim settingsSaveGate = new(1, 1);
    private PamExecutionResult? lastExecutionResult;

    [ObservableProperty] private bool areCredentialsConfigured;
    [ObservableProperty] private bool isRequestConfigured;
    [ObservableProperty] private bool isMailConfigured;
    [ObservableProperty] private bool isTestModeEnabled;
    [ObservableProperty] private bool sendEmailAtEnd;
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
        MailSettingsValidator? mailSettingsValidator = null,
        IOutlookAccountSettingsStore? outlookAccountSettingsStore = null,
        ILogger<MainWindowViewModel>? logger = null,
        IDiagnosticExportService? diagnosticExportService = null,
        IApplicationUpdateService? updateService = null,
        Version? applicationVersion = null,
        IPamInteractiveAccessService? interactiveAccessService = null)
    {
        this.executionService = executionService;
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        this.dialogs = dialogs;
        this.configurationDialogs = configurationDialogs;
        this.mailSettingsStore = mailSettingsStore;
        this.outlookAccountSettingsStore = outlookAccountSettingsStore;
        this.mailSettingsValidator = mailSettingsValidator ?? new MailSettingsValidator();
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? NullLogger<MainWindowViewModel>.Instance;
        this.diagnosticExportService = diagnosticExportService;
        this.updateService = updateService;
        this.interactiveAccessService = interactiveAccessService;
        this.applicationVersion = applicationVersion ??
            typeof(MainWindowViewModel).Assembly.GetName().Version ?? new Version(0, 0, 1, 0);

        ConfigureCredentialsCommand = new AsyncRelayCommand(ConfigureCredentialsAsync, () => AreControlsEnabled);
        ConfigureRequestCommand = new AsyncRelayCommand(ConfigureRequestAsync, () => AreControlsEnabled);
        ConfigureMailCommand = new AsyncRelayCommand(ConfigureMailAsync, () => AreControlsEnabled);
        StartTestCommand = new AsyncRelayCommand(StartTestAsync, () => IsTestActionEnabled);
        StartAutomationCommand = new AsyncRelayCommand(StartAutomationAsync, () => CanStartAutomation);
        ToggleFinalActionCommand = new AsyncRelayCommand(ToggleFinalActionAsync, () => AreControlsEnabled);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync, () => CanExportDiagnostics);
        AccessPamCommand = new AsyncRelayCommand(AccessPamAsync, () => CanAccessPam);
        ShowResultCommand = new RelayCommand(ShowResult, () => CanShowResult);
        DismissUpdateCommand = new RelayCommand(() => IsUpdateAvailable = false);
    }

    public IAsyncRelayCommand ConfigureCredentialsCommand { get; }
    public IRelayCommand ConfigureRequestCommand { get; }
    public IRelayCommand ConfigureMailCommand { get; }
    public IAsyncRelayCommand StartTestCommand { get; }
    public IAsyncRelayCommand StartAutomationCommand { get; }
    public IAsyncRelayCommand ToggleFinalActionCommand { get; }
    public IAsyncRelayCommand ExportDiagnosticsCommand { get; }
    public IAsyncRelayCommand AccessPamCommand { get; }
    public IRelayCommand ShowResultCommand { get; }
    public IRelayCommand DismissUpdateCommand { get; }
    public string ApplicationDisplayName => $"PAM Automation V. {applicationVersion.ToString(3)}";
    public bool AreControlsEnabled => !IsRunning && !IsExportingDiagnostics && !IsAccessingPam;
    public bool IsBusy => IsRunning || IsAccessingPam;
    public bool CanExportDiagnostics => diagnosticExportService is not null && dialogs is not null && AreControlsEnabled;
    public bool CanAccessPam => interactiveAccessService is not null && credentialStore is not null &&
                                AreCredentialsConfigured && !IsAccessingPam && !IsExportingDiagnostics;
    public bool CanShowResult => dialogs is not null && HasResult && AreControlsEnabled;
    public bool IsConfigurationComplete => AreCredentialsConfigured && IsRequestConfigured && (!SendEmailAtEnd || IsMailConfigured);
    public bool CanStartAutomation => AreControlsEnabled && IsConfigurationComplete;
    public bool IsTestActionVisible => IsTestModeEnabled && IsConfigurationComplete;
    public bool IsTestActionEnabled => AreControlsEnabled && IsTestActionVisible;
    public bool IsStartAutomationWarningVisible => AreControlsEnabled && !IsConfigurationComplete;
    public string StartAutomationWarningMessage => GetStartAutomationWarningMessage();
    public string ConfigurationSummaryText => IsConfigurationComplete ? "Configuración completa" : "Configuración pendiente";
    public string CredentialsActionText => AreCredentialsConfigured ? "Editar credenciales" : "Configurar credenciales";
    public string RequestActionText => IsRequestConfigured ? "Editar solicitud" : "Configurar solicitud";
    public string MailActionText => IsMailConfigured ? "Editar envío de correo" : "Configurar envío de correo";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (settingsStore is null || credentialStore is null)
        {
            return;
        }

        logger.LogInformation("Main window configuration initialization started.");
        try
        {
            settings = await settingsStore.LoadAsync(cancellationToken);
            AreCredentialsConfigured = credentialStore.HasCredentials();
            IsRequestConfigured = settingsValidator.Validate(settings, GetEcuadorNow()).IsValid;
            IsTestModeEnabled = settings.TestModeEnabled;
            SendEmailAtEnd = settings.FinalAction == FinalAction.SendEmail;
            mailSettings = mailSettingsStore is null ? null : await mailSettingsStore.LoadAsync(cancellationToken);
            if (mailSettings is not null) mailSettings = mailSettingsValidator.ValidateAndNormalize(mailSettings);
            OutlookAccountSettings? outlookAccount = outlookAccountSettingsStore is null
                ? null
                : await outlookAccountSettingsStore.LoadAsync(cancellationToken);
            if (outlookAccount is null && mailSettings is not null && outlookAccountSettingsStore is not null)
            {
                outlookAccount = new OutlookAccountSettings { SmtpAddress = mailSettings.SmtpAddress };
                await outlookAccountSettingsStore.SaveAsync(outlookAccount, cancellationToken);
            }
            outlookSmtpAddress = outlookAccount?.SmtpAddress ?? mailSettings?.SmtpAddress ?? string.Empty;
            if (mailSettings is not null && !string.IsNullOrWhiteSpace(outlookSmtpAddress))
            {
                mailSettings = mailSettingsValidator.ValidateAndNormalize(
                    mailSettings with { SmtpAddress = outlookSmtpAddress });
            }
            IsMailConfigured = mailSettings is not null;
            StatusMessage = IsConfigurationComplete
                ? "La configuración está lista para ejecutar."
                : "Completa o actualiza la configuración para habilitar la automatización.";
            RefreshPresentationState();
            logger.LogInformation("Main window configuration initialization completed. Complete={IsComplete}.", IsConfigurationComplete);
        }
        catch (Exception exception)
        {
            PresentError(exception);
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
            credentials = credentialStore!.LoadForAuthentication();
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

        if (credentials is null)
        {
            AreCredentialsConfigured = false;
            const string message = "No hay credenciales PAM configuradas.";
            if (preserveAutomationStatus)
            {
                dialogs?.ShowError(message);
            }
            else
            {
                StatusMessage = message;
            }
            return;
        }

        IsAccessingPam = true;
        if (!preserveAutomationStatus)
        {
            StatusMessage = "Abriendo BeyondInsight e iniciando sesión…";
        }
        logger.LogInformation("Interactive PAM access started.");
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

    private async Task StartAutomationAsync()
    {
        if (!ValidateRequestAtExecutionBoundary(confirmationWasShown: false)) return;
        if (!TryGetExecutionDependencies(out PamCredentials? credentials)) return;

        if (!dialogs!.ConfirmAutomationStart(settings, mailSettings))
        {
            logger.LogInformation("Real PAM execution was cancelled before preparation.");
            StatusMessage = "Ejecución cancelada. No se inició ninguna validación ni se creó ninguna solicitud.";
            return;
        }

        if (!ValidateRequestAtExecutionBoundary(confirmationWasShown: true)) return;

        IsRunning = true;
        logger.LogInformation("Real PAM execution started.");
        HasResult = false;
        StatusMessage = "Validando Favorites y Pending en segundo plano…";
        try
        {
            PamExecutionPreparation preparation = await executionService!.PrepareRealAsync(
                credentials!, settings, GetEcuadorNow());
            if (preparation.HasExactPendingDuplicates &&
                !dialogs!.ConfirmRepeatedPendingRequests(preparation))
            {
                logger.LogInformation(
                    "Real PAM execution was cancelled because exact Pending duplicates were found. DuplicateCount={DuplicateCount}.",
                    preparation.ExactPendingDuplicateCount);
                StatusMessage = "Ejecución cancelada. Las solicitudes repetidas de Pending no se volvieron a crear.";
                return;
            }

            var progress = new Progress<PamRequestSubmissionProgress>(PresentSubmissionProgress);
            PamExecutionResult result = await executionService.ExecuteRealAsync(
                credentials!, preparation, GetEcuadorNow(), mailSettings, progress: progress);
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
            outlookSmtpAddress = state.OutlookSmtpAddress;
            if (mailSettings is not null && !string.IsNullOrWhiteSpace(outlookSmtpAddress))
            {
                mailSettings = mailSettingsValidator.ValidateAndNormalize(
                    mailSettings with { SmtpAddress = outlookSmtpAddress });
            }
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

        RequestConfigurationEdit? edit = configurationDialogs.EditRequest(
            settings.Request,
            settings.TestModeEnabled,
            DateOnly.FromDateTime(GetEcuadorNow().DateTime));
        if (edit is null)
        {
            logger.LogInformation("Request configuration dialog was cancelled.");
            return;
        }

        AppSettings proposed = settings with
        {
            Request = edit.Request,
            TestModeEnabled = edit.TestModeEnabled,
            FinalAction = SendEmailAtEnd ? FinalAction.SendEmail : FinalAction.DisplayRequestIds
        };
        ConfigurationValidationResult validation = settingsValidator.Validate(proposed, GetEcuadorNow());
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

    private async Task StartTestAsync()
    {
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

    private async Task ConfigureMailAsync()
    {
        if (configurationDialogs is null || mailSettingsStore is null)
        {
            ShowPendingFeature("La configuración de correo no está disponible en este contexto.");
            return;
        }

        if (string.IsNullOrWhiteSpace(outlookSmtpAddress))
        {
            dialogs?.ShowError("Configura y verifica primero tu cuenta de Outlook en Editar credenciales.");
            return;
        }

        MailConfigurationEdit? edit = configurationDialogs.EditMail(mailSettings, outlookSmtpAddress);
        if (edit is null)
        {
            logger.LogInformation("Mail configuration dialog was cancelled.");
            return;
        }
        IsRunning = true;
        StatusMessage = "Validando y guardando la configuración de correo…";
        try
        {
            MailSettings normalized = mailSettingsValidator.ValidateAndNormalize(
                edit.Settings with { SmtpAddress = outlookSmtpAddress });
            await mailSettingsStore.SaveAsync(normalized);
            mailSettings = normalized;
            IsMailConfigured = true;
            StatusMessage = "Configuración de correo validada y guardada de forma protegida.";
            logger.LogInformation("Mail configuration was stored successfully.");
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
        IsRequestConfigured = settingsValidator.Validate(settings, GetEcuadorNow()).IsValid;
        RefreshPresentationState();

        if (wasRequestConfigured && !IsRequestConfigured && !IsRunning)
        {
            StatusMessage = "La fecha u hora de inicio configurada ya pasó. Abre Configurar solicitud para actualizarla.";
            logger.LogInformation("The configured request start time expired while the application remained open.");
        }
    }

    private bool ValidateRequestAtExecutionBoundary(bool confirmationWasShown)
    {
        ConfigurationValidationResult validation = settingsValidator.Validate(settings, GetEcuadorNow());
        IsRequestConfigured = validation.IsValid;
        RefreshPresentationState();
        if (validation.IsValid) return true;

        ConfigurationValidationFailure? timeFailure = validation.Failures.FirstOrDefault(failure =>
            failure.Code is ConfigurationValidationErrorCode.StartDateInPast or
                ConfigurationValidationErrorCode.StartTimeInPast);
        string message = timeFailure is not null && confirmationWasShown
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
        ResultTitle = result.Mode == PamExecutionMode.Test ? "Prueba completada" : "Automatización completada";
        ResultSummary = result.Mode == PamExecutionMode.Test
            ? $"{result.ProcessedCount} de {result.FavoriteCount} formularios fueron validados y descartados. Solicitudes creadas: 0. Finalizó: {result.CompletedAt:dd/MM/yyyy hh:mm tt}."
            : $"Procesadas: {result.ProcessedCount} de {result.FavoriteCount}. Request ID: {result.RequestIds.Count}. Conflictos: {result.ConflictCount}. Finalizó: {result.CompletedAt:dd/MM/yyyy hh:mm tt}.";
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
        dialogs!.ShowResult(ResultTitle, message);
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
        PamAuthenticationException or PamFavoritesException or PamRequestSubmissionException or
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

        if (!IsRequestConfigured)
        {
            ConfigurationValidationFailure? failure = settingsValidator
                .Validate(settings, GetEcuadorNow())
                .Failures
                .FirstOrDefault();

            return failure?.Code switch
            {
                ConfigurationValidationErrorCode.StartDateInPast =>
                    "El botón está desactivado porque la fecha de inicio configurada ya pasó. Abre Configurar solicitud y elige una fecha y hora futuras.",
                ConfigurationValidationErrorCode.StartTimeInPast =>
                    "El botón está desactivado porque la hora de inicio configurada ya pasó. Abre Configurar solicitud y elige una fecha y hora futuras.",
                _ when failure is not null =>
                    $"El botón está desactivado porque la solicitud no es válida: {failure.Message} Abre Configurar solicitud para corregirla.",
                _ =>
                    "El botón está desactivado porque falta configurar la solicitud. Abre Configurar solicitud para completarla."
            };
        }

        if (SendEmailAtEnd && !IsMailConfigured)
        {
            return "El botón está desactivado porque elegiste enviar el resultado por Outlook y falta configurar el correo. Abre Configurar envío de correo.";
        }

        return string.Empty;
    }

    private void RefreshPresentationState()
    {
        OnPropertyChanged(nameof(AreControlsEnabled));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanExportDiagnostics));
        OnPropertyChanged(nameof(CanAccessPam));
        OnPropertyChanged(nameof(CanShowResult));
        OnPropertyChanged(nameof(IsConfigurationComplete));
        OnPropertyChanged(nameof(CanStartAutomation));
        OnPropertyChanged(nameof(IsTestActionVisible));
        OnPropertyChanged(nameof(IsTestActionEnabled));
        OnPropertyChanged(nameof(IsStartAutomationWarningVisible));
        OnPropertyChanged(nameof(StartAutomationWarningMessage));
        OnPropertyChanged(nameof(ConfigurationSummaryText));
        OnPropertyChanged(nameof(CredentialsActionText));
        OnPropertyChanged(nameof(RequestActionText));
        OnPropertyChanged(nameof(MailActionText));
        ConfigureCredentialsCommand.NotifyCanExecuteChanged();
        ConfigureRequestCommand.NotifyCanExecuteChanged();
        ConfigureMailCommand.NotifyCanExecuteChanged();
        StartTestCommand.NotifyCanExecuteChanged();
        StartAutomationCommand.NotifyCanExecuteChanged();
        ToggleFinalActionCommand.NotifyCanExecuteChanged();
        ExportDiagnosticsCommand.NotifyCanExecuteChanged();
        AccessPamCommand.NotifyCanExecuteChanged();
        ShowResultCommand.NotifyCanExecuteChanged();
    }

}
