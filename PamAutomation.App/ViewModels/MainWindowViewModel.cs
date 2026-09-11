using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Services;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Diagnostics;
using PamAutomation.Core.Interfaces.Execution;
using PamAutomation.Core.Interfaces.Outlook;
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
    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private bool hasRequestIds;
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
        IDiagnosticExportService? diagnosticExportService = null)
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

        ConfigureCredentialsCommand = new AsyncRelayCommand(ConfigureCredentialsAsync, () => AreControlsEnabled);
        ConfigureRequestCommand = new AsyncRelayCommand(ConfigureRequestAsync, () => AreControlsEnabled);
        ConfigureMailCommand = new AsyncRelayCommand(ConfigureMailAsync, () => AreControlsEnabled);
        StartTestCommand = new AsyncRelayCommand(StartTestAsync, () => IsTestActionEnabled);
        StartAutomationCommand = new AsyncRelayCommand(StartAutomationAsync, () => CanStartAutomation);
        ToggleFinalActionCommand = new AsyncRelayCommand(ToggleFinalActionAsync, () => AreControlsEnabled);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync, () => CanExportDiagnostics);
    }

    public IAsyncRelayCommand ConfigureCredentialsCommand { get; }
    public IRelayCommand ConfigureRequestCommand { get; }
    public IRelayCommand ConfigureMailCommand { get; }
    public IAsyncRelayCommand StartTestCommand { get; }
    public IAsyncRelayCommand StartAutomationCommand { get; }
    public IAsyncRelayCommand ToggleFinalActionCommand { get; }
    public IAsyncRelayCommand ExportDiagnosticsCommand { get; }
    public bool AreControlsEnabled => !IsRunning && !IsExportingDiagnostics;
    public bool CanExportDiagnostics => diagnosticExportService is not null && dialogs is not null && AreControlsEnabled;
    public bool IsConfigurationComplete => AreCredentialsConfigured && IsRequestConfigured && (!SendEmailAtEnd || IsMailConfigured);
    public bool CanStartAutomation => AreControlsEnabled && IsConfigurationComplete;
    public bool IsTestActionVisible => IsTestModeEnabled && IsConfigurationComplete;
    public bool IsTestActionEnabled => AreControlsEnabled && IsTestActionVisible;
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
            logger.LogInformation("Main window configuration initialization completed. Complete={IsComplete}.", IsConfigurationComplete);
        }
        catch (Exception exception)
        {
            PresentError(exception);
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

    private async Task StartAutomationAsync()
    {
        if (!TryGetExecutionDependencies(out PamCredentials? credentials)) return;

        IsRunning = true;
        logger.LogInformation("Real PAM execution started.");
        HasResult = false;
        StatusMessage = "Leyendo Favorites para preparar la confirmación…";
        try
        {
            PamExecutionPreparation preparation = await executionService!.PrepareRealAsync(
                credentials!, settings, GetEcuadorNow());
            if (!dialogs!.ConfirmRealExecution(preparation, mailSettings))
            {
                logger.LogInformation("Real PAM execution was cancelled before submission.");
                StatusMessage = "Ejecución cancelada. No se creó ninguna solicitud.";
                return;
            }

            StatusMessage = "Creando solicitudes y reconciliando Pending…";
            PamExecutionResult result = await executionService.ExecuteRealAsync(
                credentials!, preparation, GetEcuadorNow(), mailSettings);
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
        dialogs!.ShowResult(ResultTitle, ResultSummary);
    }

    private void PresentError(Exception exception)
    {
        logger.LogError(exception, "Application operation failed.");
        string message = exception switch
        {
            PamAuthenticationException or PamFavoritesException or PamRequestSubmissionException or
                PamPendingException or PamRequestTestException or OutlookValidationException or
                OutlookOperationException or OutlookAccountResolutionException => exception.Message,
            OperationCanceledException => "La operación fue cancelada.",
            _ => "Ocurrió un error no esperado. No se muestran detalles que puedan contener información sensible.",
        };
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

    private void RefreshPresentationState()
    {
        OnPropertyChanged(nameof(AreControlsEnabled));
        OnPropertyChanged(nameof(CanExportDiagnostics));
        OnPropertyChanged(nameof(IsConfigurationComplete));
        OnPropertyChanged(nameof(CanStartAutomation));
        OnPropertyChanged(nameof(IsTestActionVisible));
        OnPropertyChanged(nameof(IsTestActionEnabled));
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
    }
}
