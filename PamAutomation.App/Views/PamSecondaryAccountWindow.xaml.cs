using System.Windows;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Views;

public partial class PamSecondaryAccountWindow : Window
{
    public PamSecondaryAccountWindow(PamAccountSummary? account = null)
    {
        InitializeComponent();
        IsEditing = account is not null;
        if (account is not null)
        {
            Title = "Editar usuario PAM";
            HeadingText.Text = "Editar usuario PAM";
            NameBox.Text = account.Name;
            UsernameBox.Text = string.Empty;
            EditHelpText.Visibility = Visibility.Visible;
        }
        Loaded += (_, _) => NameBox.Focus();
    }

    public bool IsEditing { get; }
    public string AccountName { get; private set; } = string.Empty;
    public PamCredentialUpdate CredentialUpdate { get; private set; } = new();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AccountName = PamAccountSummary.NormalizeName(NameBox.Text);
            if (!IsEditing && (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password) || string.IsNullOrWhiteSpace(TotpSeedBox.Password)))
            {
                ValidationText.Text = "La primera configuración requiere usuario, contraseña y semilla TOTP.";
                return;
            }
            CredentialUpdate = new PamCredentialUpdate
            {
                Username = UsernameBox.Text,
                Password = PasswordBox.Password,
                TotpSeed = TotpSeedBox.Password
            };
            DialogResult = true;
        }
        catch (ArgumentException exception) { ValidationText.Text = exception.Message; }
    }
}
