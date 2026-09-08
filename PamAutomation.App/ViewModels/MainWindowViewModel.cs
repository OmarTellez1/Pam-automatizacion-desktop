using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PamAutomation.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool areCredentialsConfigured;

    [ObservableProperty]
    private bool isRequestConfigured;

    [ObservableProperty]
    private bool isMailConfigured;

    [ObservableProperty]
    private bool isTestModeEnabled;

    [ObservableProperty]
    private bool sendEmailAtEnd;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string statusMessage = "Completa la configuración para habilitar la automatización.";

    public MainWindowViewModel()
    {
        ConfigureCredentialsCommand = new RelayCommand(
            () => ShowPendingFeature("La configuración segura de credenciales se implementará en la siguiente fase."),
            () => AreControlsEnabled);

        ConfigureRequestCommand = new RelayCommand(
            () => ShowPendingFeature("La configuración de la solicitud PAM se implementará en la siguiente fase."),
            () => AreControlsEnabled);

        ConfigureMailCommand = new RelayCommand(
            () => ShowPendingFeature("La configuración de correo se implementará en la siguiente fase."),
            () => AreControlsEnabled);

        StartTestCommand = new RelayCommand(
            () => ShowPendingFeature("La ejecución de prueba se habilitará cuando exista el flujo de automatización."),
            () => IsTestActionEnabled);

        StartAutomationCommand = new RelayCommand(
            () => ShowPendingFeature("La ejecución se conectará a PAM, Edge y Outlook en fases posteriores."),
            () => CanStartAutomation);
    }

    public IRelayCommand ConfigureCredentialsCommand { get; }

    public IRelayCommand ConfigureRequestCommand { get; }

    public IRelayCommand ConfigureMailCommand { get; }

    public IRelayCommand StartTestCommand { get; }

    public IRelayCommand StartAutomationCommand { get; }

    public bool AreControlsEnabled => !IsRunning;

    public bool IsConfigurationComplete =>
        AreCredentialsConfigured &&
        IsRequestConfigured &&
        (!SendEmailAtEnd || IsMailConfigured);

    public bool CanStartAutomation => AreControlsEnabled && IsConfigurationComplete;

    public bool IsTestActionVisible => IsTestModeEnabled && IsConfigurationComplete;

    public bool IsTestActionEnabled => AreControlsEnabled && IsTestActionVisible;

    public string ConfigurationSummaryText =>
        IsConfigurationComplete ? "Configuración completa" : "Configuración pendiente";

    public string CredentialsActionText =>
        AreCredentialsConfigured ? "Editar credenciales" : "Configurar credenciales";

    public string RequestActionText =>
        IsRequestConfigured ? "Editar solicitud" : "Configurar solicitud";

    public string MailActionText =>
        IsMailConfigured ? "Editar correo" : "Configurar correo";

    partial void OnAreCredentialsConfiguredChanged(bool value) => RefreshPresentationState();

    partial void OnIsRequestConfiguredChanged(bool value) => RefreshPresentationState();

    partial void OnIsMailConfiguredChanged(bool value) => RefreshPresentationState();

    partial void OnIsTestModeEnabledChanged(bool value) => RefreshPresentationState();

    partial void OnSendEmailAtEndChanged(bool value) => RefreshPresentationState();

    partial void OnIsRunningChanged(bool value) => RefreshPresentationState();

    private void ShowPendingFeature(string message)
    {
        StatusMessage = message;
    }

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
    }
}
