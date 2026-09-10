using System.Windows;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Views;

public partial class CredentialsWindow : Window
{
    private readonly bool hasExistingCredentials;

    public CredentialsWindow(bool hasExistingCredentials)
    {
        InitializeComponent();
        this.hasExistingCredentials = hasExistingCredentials;
        HelpText.Text = hasExistingCredentials
            ? "Deja cualquier campo vacío para conservar su valor protegido actual. Los secretos nunca se vuelven a mostrar."
            : "La primera configuración requiere los tres campos. Se guardarán en Windows Credential Manager.";
    }

    public PamCredentialUpdate? Update { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!hasExistingCredentials &&
            (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password) ||
             string.IsNullOrWhiteSpace(TotpSeedBox.Password)))
        {
            MessageBox.Show("Completa usuario, contraseña y clave Base32.", "Credenciales incompletas",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Update = new PamCredentialUpdate
        {
            Username = UsernameBox.Text,
            Password = PasswordBox.Password,
            TotpSeed = TotpSeedBox.Password
        };
        DialogResult = true;
    }
}
