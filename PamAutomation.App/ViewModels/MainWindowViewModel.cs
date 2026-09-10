using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PamAutomation.App.Services;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Interfaces.Credentials;
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
    private readonly IOutlookClassicClient? outlookClient;
    private readonly MailSettingsValidator mailSettingsValidator;
    private readonly AppSettingsValidator settingsValidator;
    private readonly TimeProvider timeProvider;
    private AppSettings settings = new();
    private MailSettings? mailSettings;
    private readonly SemaphoreSlim settingsSaveGate = new(1, 1);

    [ObservableProperty] private bool areCredentialsConfigured;
    [ObservableProperty] private bool isRequestConfigured;
    [ObservableProperty] private bool isMailConfigured;
    [ObservableProperty] private bool isTestModeEnabled;
    [ObservableProperty] private bool sendEmailAtEnd;
    [ObservableProperty] private bool isRunning;
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
        IOutlookClassicClient? outlookClient = null,
        MailSettingsValidator? mailSettingsValidator = null)
    {
        this.executionService = executionService;
        this.settingsStore = settingsStore;
        this.credentialStore = credentialStore;
        this.dialogs = dialogs;
        this.configurationDialogs = configurationDialogs;
        this.mailSettingsStore = mailSettingsStore;
        this.outlookClient = outlookClient;
        this.mailSettingsValidator = mailSettingsValidator ?? new MailSettingsValidator();
        this.settingsValidator = settingsValidator ?? throw new ArgumentNullException(nameof(settingsValidator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        ConfigureCredentialsCommand = new RelayCommand(ConfigureCredentials, () => AreControlsEnabled);
        ConfigureRequestCommand = new AsyncRelayCommand(ConfigureRequestAsync, () => AreControlsEnabled);
        ConfigureMailCommand = new AsyncRelayCommand(ConfigureMailAsync, () => AreControlsEnabled);
        StartTestCommand = new AsyncRelayCommand(StartTestAsync, () => IsTestActionEnabled);
        StartAutomationCommand = new AsyncRelayCommand(StartAutomationAsync, () => CanStartAutomation);
        ToggleFinalActionCommand = new AsyncRelayCommand(ToggleFinalActionAsync, () => AreControlsEnabled);
    }

    public IRelayCommand ConfigureCredentialsCommand { get; }
    public IRelayCommand ConfigureRequestCommand { get; }
    public IRelayCommand ConfigureMailCommand { get; }
    public IAsyncRelayCommand StartTestCommand { get; }
    public IAsyncRelayCommand StartAutomationCommand { get; }
    public IAsyncRelayCommand ToggleFinalActionCommand { get; }
    public bool AreControlsEnabled => !IsRunning;
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

        try
        {
            settings = await settingsStore.LoadAsync(cancellationToken);
            AreCredentialsConfigured = credentialStore.HasCredentials();
            IsRequestConfigured = settingsValidator.Validate(settings, GetEcuadorNow()).IsValid;
            IsTestModeEnabled = settings.TestModeEnabled;
            SendEmailAtEnd = settings.FinalAction == FinalAction.SendEmail;
            mailSettings = mailSettingsStore is null ? null : await mailSettingsStore.LoadAsync(cancellationToken);
            if (mailSettings is not null) mailSettings = mailSettingsValidator.ValidateAndNormalize(mailSettings);
            IsMailConfigured = mailSettings is not null;
            StatusMessage = IsConfigurationComplete
                ? "La configuración está lista para ejecutar."
                : "Completa o actualiza la configuración para habilitar la automatización.";
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

    private async Task StartAutomationAsync()
    {
        if (!TryGetExecutionDependencies(out PamCredentials? credentials)) return;

        IsRunning = true;
        HasResult = false;
        StatusMessage = "Leyendo Favorites para preparar la confirmación…";
        try
        {
            PamExecutionPreparation preparation = await executionService!.PrepareRealAsync(
                credentials!, settings, GetEcuadorNow());
            if (!dialogs!.ConfirmRealExecution(preparation, mailSettings))
            {
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

    private void ConfigureCredentials()
    {
        if (credentialStore is null || configurationDialogs is null)
        {
            ShowPendingFeature("La configuración de credenciales no está disponible en este contexto.");
            return;
        }

        try
        {
            PamCredentialUpdate? update = configurationDialogs.EditCredentials(AreCredentialsConfigured);
            if (update is null) return;
            credentialStore.Save(update);
            AreCredentialsConfigured = credentialStore.HasCredentials();
            StatusMessage = "Credenciales guardadas de forma segura en Windows Credential Manager.";
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
        if (edit is null) return;

        AppSettings proposed = settings with
        {
            Request = edit.Request,
            TestModeEnabled = edit.TestModeEnabled,
            FinalAction = SendEmailAtEnd ? FinalAction.SendEmail : FinalAction.DisplayRequestIds
        };
        ConfigurationValidationResult validation = settingsValidator.Validate(proposed, GetEcuadorNow());
        if (!validation.IsValid)
        {
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
        if (configurationDialogs is null || mailSettingsStore is null || outlookClient is null)
        {
            ShowPendingFeature("La configuración de correo no está disponible en este contexto.");
            return;
        }

        MailConfigurationEdit? edit = configurationDialogs.EditMail(mailSettings);
        if (edit is null) return;
        IsRunning = true;
        StatusMessage = "Validando la cuenta en Outlook clásico sin enviar correo…";
        try
        {
            MailSettings normalized = mailSettingsValidator.ValidateAndNormalize(edit.Settings);
            await outlookClient.ValidateAccountAsync(normalized.SmtpAddress);
            await mailSettingsStore.SaveAsync(normalized);
            mailSettings = normalized;
            IsMailConfigured = true;
            StatusMessage = "Configuración de correo validada y guardada de forma protegida.";
            if (edit.SendTestRequested && dialogs?.ConfirmOutlookTestEmail(normalized.SmtpAddress) == true)
            {
                string body = "<p>Prueba de configuración de Pam Automation.</p>";
                OutlookDeliveryResult result = await outlookClient.SendAsync(new OutlookSendRequest(
                    normalized.SmtpAddress,
                    "[PRUEBA OUTLOOK] Pam Automation",
                    body,
                    [normalized.SmtpAddress],
                    [],
                    []));
                StatusMessage = result.Message;
            }
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
        StatusMessage = message;
        dialogs?.ShowError(message);
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
    }
}
