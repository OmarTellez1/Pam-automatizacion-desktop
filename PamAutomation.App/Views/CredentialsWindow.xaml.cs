using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Views;

public partial class CredentialsWindow : Window
{
    private readonly ICredentialConfigurationWorkflow workflow;
    private readonly CancellationTokenSource operationCancellation;
    private readonly ILogger<CredentialsWindow> logger;
    private bool synchronizingSecret;

    public CredentialsWindow(
        CredentialConfigurationState current,
        ICredentialConfigurationWorkflow workflow,
        CancellationToken cancellationToken,
        ILogger<CredentialsWindow>? logger = null)
    {
        InitializeComponent();
        CurrentState = current ?? throw new ArgumentNullException(nameof(current));
        this.workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        this.logger = logger ?? NullLogger<CredentialsWindow>.Instance;
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        OutlookSmtpBox.Text = current.OutlookSmtpAddress;
        RefreshConfiguredState();
    }

    public CredentialConfigurationState CurrentState { get; private set; }

    private async void SavePam_Click(object sender, RoutedEventArgs e)
    {
        string password = GetPassword();
        string totpSeed = GetTotpSeed();
        if (!CurrentState.HasPamCredentials &&
            (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(password) ||
             string.IsNullOrWhiteSpace(totpSeed)))
        {
            ShowOperation(PamOperationBorder, PamOperationText, PamOperationSpinner, false,
                "Completa usuario, contraseña y clave alfanumérica para la primera configuración.");
            return;
        }

        await RunOperationAsync(
            PamOperationBorder,
            PamOperationText,
            PamOperationSpinner,
            "Solicitud recibida. Guardando credenciales y comprobando acceso al PAM…",
            async () =>
            {
                ConfigurationOperationResult result = await workflow.SaveAndTestPamAsync(
                    new PamCredentialUpdate
                    {
                        Username = UsernameBox.Text,
                        Password = password,
                        TotpSeed = totpSeed
                    },
                    operationCancellation.Token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    await UpdateInterfaceSafelyAsync(
                        () =>
                        {
                            CurrentState = CurrentState with { HasPamCredentials = true };
                            ClearPamInputs();
                            RefreshConfiguredState();
                        },
                        "PamStateRefresh").ConfigureAwait(false);
                }
                return result;
            });
    }

    private async void VerifyOutlook_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(
            OutlookOperationBorder,
            OutlookOperationText,
            OutlookOperationSpinner,
            "Solicitud recibida. Comprobando la cuenta en Outlook clásico…",
            async () =>
            {
                ConfigurationOperationResult result = await workflow.SaveAndVerifyOutlookAsync(
                    OutlookSmtpBox.Text,
                    operationCancellation.Token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    await UpdateInterfaceSafelyAsync(
                        () =>
                        {
                            CurrentState = CurrentState with
                            {
                                HasOutlookAccount = true,
                                OutlookSmtpAddress = OutlookSmtpBox.Text.Trim()
                            };
                            RefreshConfiguredState();
                        },
                        "OutlookStateRefresh").ConfigureAwait(false);
                }
                return result;
            });
    }

    private async void TestOutlook_Click(object sender, RoutedEventArgs e)
    {
        string smtpAddress = OutlookSmtpBox.Text.Trim();
        if (!CurrentState.HasOutlookAccount ||
            !string.Equals(CurrentState.OutlookSmtpAddress, smtpAddress, StringComparison.OrdinalIgnoreCase))
        {
            ShowOperation(OutlookOperationBorder, OutlookOperationText, OutlookOperationSpinner, false,
                "Guarda y verifica primero esta cuenta de Outlook.");
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            $"Se enviará un correo de prueba desde y hacia {smtpAddress}. ¿Deseas continuar?",
            "Confirmar correo de prueba",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes) return;

        await RunOperationAsync(
            OutlookOperationBorder,
            OutlookOperationText,
            OutlookOperationSpinner,
            "Solicitud recibida. Outlook está preparando el correo de prueba…",
            () => workflow.SendOutlookTestAsync(smtpAddress, operationCancellation.Token));
    }

    private async Task RunOperationAsync(
        Border resultBorder,
        TextBlock resultText,
        FrameworkElement progressIndicator,
        string progressMessage,
        Func<Task<ConfigurationOperationResult>> operation)
    {
        if (!TryStartOperation(resultBorder, resultText, progressIndicator, progressMessage))
        {
            return;
        }

        try
        {
            ConfigurationOperationResult result = await operation().ConfigureAwait(false);
            await UpdateInterfaceSafelyAsync(
                () => ShowOperation(resultBorder, resultText, progressIndicator, result.IsSuccess, result.Message),
                "ResultPresentation").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await UpdateInterfaceSafelyAsync(
                () => ShowOperation(
                    resultBorder,
                    resultText,
                    progressIndicator,
                    false,
                    "La operación fue cancelada."),
                "CancellationPresentation").ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Credential configuration UI operation failed unexpectedly.");
            await UpdateInterfaceSafelyAsync(
                () => ShowOperation(
                    resultBorder,
                    resultText,
                    progressIndicator,
                    false,
                    "Ocurrió un problema inesperado. La operación se detuvo de forma segura; revisa el registro e inténtalo nuevamente."),
                "UnexpectedFailurePresentation").ConfigureAwait(false);
        }
        finally
        {
            await UpdateInterfaceSafelyAsync(
                () => SetControlsEnabled(true),
                "ControlRestoration").ConfigureAwait(false);
        }
    }

    private bool TryStartOperation(
        Border resultBorder,
        TextBlock resultText,
        FrameworkElement progressIndicator,
        string progressMessage)
    {
        try
        {
            SetControlsEnabled(false);
            ShowProgress(resultBorder, resultText, progressIndicator, progressMessage);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Credential configuration UI operation could not be started.");
            try
            {
                StopProgressIndicator(progressIndicator);
                SetControlsEnabled(true);
            }
            catch (Exception recoveryException)
            {
                logger.LogError(recoveryException, "Credential configuration UI recovery failed at operation start.");
            }

            return false;
        }
    }

    private async Task UpdateInterfaceSafelyAsync(Action update, string stage)
    {
        try
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                logger.LogWarning("Credential configuration UI update skipped because its dispatcher is shutting down. Stage={Stage}.", stage);
                return;
            }

            await Dispatcher.InvokeAsync(update);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Credential configuration UI update failed at {Stage}.", stage);
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        SavePamButton.IsEnabled = enabled;
        VerifyOutlookButton.IsEnabled = enabled;
        TestOutlookButton.IsEnabled = enabled && CurrentState.HasOutlookAccount;
    }

    private void ShowProgress(Border border, TextBlock text, FrameworkElement indicator, string message)
    {
        border.Visibility = Visibility.Visible;
        border.Background = new SolidColorBrush(Color.FromRgb(235, 240, 255));
        text.Foreground = new SolidColorBrush(Color.FromRgb(52, 89, 217));
        text.Text = message;
        StartProgressIndicator(indicator);
    }

    private void ShowOperation(
        Border border,
        TextBlock text,
        FrameworkElement indicator,
        bool success,
        string message)
    {
        border.Visibility = Visibility.Visible;
        StopProgressIndicator(indicator);
        border.Background = new SolidColorBrush(success
            ? Color.FromRgb(232, 246, 240)
            : Color.FromRgb(255, 235, 238));
        text.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(24, 135, 91)
            : Color.FromRgb(195, 59, 74));
        text.Text = message;
    }

    private void StartProgressIndicator(FrameworkElement indicator)
    {
        try
        {
            var rotation = new RotateTransform();
            indicator.RenderTransformOrigin = new Point(0.5, 0.5);
            indicator.RenderTransform = rotation;
            indicator.Visibility = Visibility.Visible;
            rotation.BeginAnimation(
                RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(800))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Credential operation progress animation could not be started.");
            indicator.Visibility = Visibility.Collapsed;
            indicator.RenderTransform = Transform.Identity;
        }
    }

    private void StopProgressIndicator(FrameworkElement indicator)
    {
        try
        {
            if (indicator.RenderTransform is RotateTransform rotation)
            {
                rotation.BeginAnimation(RotateTransform.AngleProperty, null);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Credential operation progress animation could not be stopped cleanly.");
        }
        finally
        {
            indicator.Visibility = Visibility.Collapsed;
            indicator.RenderTransform = Transform.Identity;
        }
    }

    private void RefreshConfiguredState()
    {
        PamConfiguredText.Text = CurrentState.HasPamCredentials
            ? "Ya tienes credenciales del PAM configuradas. Deja vacío cualquier dato que quieras conservar."
            : "Todavía no tienes credenciales del PAM configuradas.";
        OutlookConfiguredText.Text = CurrentState.HasOutlookAccount
            ? "Tu cuenta de Outlook está configurada."
            : "Todavía no tienes una cuenta de Outlook configurada.";
        PamConfiguredBorder.Background = FindConfiguredBrush(CurrentState.HasPamCredentials);
        OutlookConfiguredBorder.Background = FindConfiguredBrush(CurrentState.HasOutlookAccount);
        PamConfiguredText.Foreground = FindConfiguredTextBrush(CurrentState.HasPamCredentials);
        OutlookConfiguredText.Foreground = FindConfiguredTextBrush(CurrentState.HasOutlookAccount);
        TestOutlookButton.IsEnabled = CurrentState.HasOutlookAccount;
    }

    private Brush FindConfiguredBrush(bool configured) => configured
        ? (Brush)FindResource("SuccessSoftBrush")
        : (Brush)FindResource("NeutralSurfaceBrush");

    private Brush FindConfiguredTextBrush(bool configured) => configured
        ? (Brush)FindResource("SuccessBrush")
        : (Brush)FindResource("TextSecondaryBrush");

    private string GetPassword() => PasswordTextBox.Visibility == Visibility.Visible
        ? PasswordTextBox.Text
        : PasswordBox.Password;

    private string GetTotpSeed() => TotpSeedTextBox.Visibility == Visibility.Visible
        ? TotpSeedTextBox.Text
        : TotpSeedBox.Password;

    private void ClearPamInputs()
    {
        UsernameBox.Clear();
        PasswordBox.Clear();
        PasswordTextBox.Clear();
        TotpSeedBox.Clear();
        TotpSeedTextBox.Clear();
    }

    private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e) =>
        ToggleSecretVisibility(PasswordBox, PasswordTextBox, PasswordVisibilityButton);

    private void ToggleTotpVisibility_Click(object sender, RoutedEventArgs e) =>
        ToggleSecretVisibility(TotpSeedBox, TotpSeedTextBox, TotpVisibilityButton);

    private static void ToggleSecretVisibility(PasswordBox passwordBox, TextBox textBox, Button button)
    {
        bool show = textBox.Visibility != Visibility.Visible;
        textBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        passwordBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        button.Content = show ? "Ocultar" : "Mostrar";
        if (show)
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
        }
        else
        {
            passwordBox.Focus();
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        SynchronizeSecret(PasswordBox.Password, PasswordTextBox, null);

    private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        SynchronizeSecret(PasswordTextBox.Text, null, PasswordBox);

    private void TotpSeedBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        SynchronizeSecret(TotpSeedBox.Password, TotpSeedTextBox, null);

    private void TotpSeedTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        SynchronizeSecret(TotpSeedTextBox.Text, null, TotpSeedBox);

    private void SynchronizeSecret(string value, TextBox? textBox, PasswordBox? passwordBox)
    {
        if (synchronizingSecret) return;
        synchronizingSecret = true;
        try
        {
            if (textBox is not null && textBox.Text != value) textBox.Text = value;
            if (passwordBox is not null && passwordBox.Password != value) passwordBox.Password = value;
        }
        finally
        {
            synchronizingSecret = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        operationCancellation.Cancel();
        operationCancellation.Dispose();
        base.OnClosed(e);
    }
}
