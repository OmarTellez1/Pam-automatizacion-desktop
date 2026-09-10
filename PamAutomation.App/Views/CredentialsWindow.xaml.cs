using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Views;

public partial class CredentialsWindow : Window
{
    private readonly ICredentialConfigurationWorkflow workflow;
    private readonly CancellationTokenSource operationCancellation;
    private bool synchronizingSecret;

    public CredentialsWindow(
        CredentialConfigurationState current,
        ICredentialConfigurationWorkflow workflow,
        CancellationToken cancellationToken)
    {
        InitializeComponent();
        CurrentState = current ?? throw new ArgumentNullException(nameof(current));
        this.workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
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
            ShowOperation(PamOperationBorder, PamOperationText, false,
                "Completa usuario, contraseña y clave alfanumérica para la primera configuración.");
            return;
        }

        await RunOperationAsync(
            PamOperationBorder,
            PamOperationText,
            "Guardando y comprobando acceso al PAM…",
            async () =>
            {
                ConfigurationOperationResult result = await workflow.SaveAndTestPamAsync(
                    new PamCredentialUpdate
                    {
                        Username = UsernameBox.Text,
                        Password = password,
                        TotpSeed = totpSeed
                    },
                    operationCancellation.Token);
                if (result.IsSuccess)
                {
                    CurrentState = CurrentState with { HasPamCredentials = true };
                    ClearPamInputs();
                    RefreshConfiguredState();
                }
                return result;
            });
    }

    private async void VerifyOutlook_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(
            OutlookOperationBorder,
            OutlookOperationText,
            "Verificando la cuenta en Outlook clásico…",
            async () =>
            {
                ConfigurationOperationResult result = await workflow.SaveAndVerifyOutlookAsync(
                    OutlookSmtpBox.Text,
                    operationCancellation.Token);
                if (result.IsSuccess)
                {
                    CurrentState = CurrentState with
                    {
                        HasOutlookAccount = true,
                        OutlookSmtpAddress = OutlookSmtpBox.Text.Trim()
                    };
                    RefreshConfiguredState();
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
            ShowOperation(OutlookOperationBorder, OutlookOperationText, false,
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
            "Enviando correo de prueba…",
            () => workflow.SendOutlookTestAsync(smtpAddress, operationCancellation.Token));
    }

    private async Task RunOperationAsync(
        Border resultBorder,
        TextBlock resultText,
        string progressMessage,
        Func<Task<ConfigurationOperationResult>> operation)
    {
        SetControlsEnabled(false);
        ShowProgress(resultBorder, resultText, progressMessage);
        try
        {
            ConfigurationOperationResult result = await operation();
            ShowOperation(resultBorder, resultText, result.IsSuccess, result.Message);
        }
        catch (OperationCanceledException)
        {
            ShowOperation(resultBorder, resultText, false, "La operación fue cancelada.");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        SavePamButton.IsEnabled = enabled;
        VerifyOutlookButton.IsEnabled = enabled;
        TestOutlookButton.IsEnabled = enabled && CurrentState.HasOutlookAccount;
    }

    private static void ShowProgress(Border border, TextBlock text, string message)
    {
        border.Visibility = Visibility.Visible;
        border.Background = new SolidColorBrush(Color.FromRgb(235, 240, 255));
        text.Foreground = new SolidColorBrush(Color.FromRgb(52, 89, 217));
        text.Text = message;
    }

    private static void ShowOperation(Border border, TextBlock text, bool success, string message)
    {
        border.Visibility = Visibility.Visible;
        border.Background = new SolidColorBrush(success
            ? Color.FromRgb(232, 246, 240)
            : Color.FromRgb(255, 235, 238));
        text.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(24, 135, 91)
            : Color.FromRgb(195, 59, 74));
        text.Text = message;
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
