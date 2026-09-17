using System.Windows;
using System.Windows.Controls;
using PamAutomation.Core.Accounts;

namespace PamAutomation.App.Views;

/// <summary>Selects one configured identity for a single interactive PAM session.</summary>
public partial class PamAccountSelectionWindow : Window
{
    public PamAccountSelectionWindow(IReadOnlyList<PamAccountStatus> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        InitializeComponent();
        AccountsList.ItemsSource = accounts
            .OrderByDescending(status => status.Account.IsPrimary)
            .ThenBy(status => status.Account.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(status => new PamAccountSelectionItem(status))
            .ToArray();
    }

    public PamAccountId? SelectedAccountId { get; private set; }

    private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContinueButton.IsEnabled = AccountsList.SelectedItem is PamAccountSelectionItem { HasCredentials: true };
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (AccountsList.SelectedItem is not PamAccountSelectionItem { HasCredentials: true } selected) return;
        SelectedAccountId = selected.Account.Id;
        DialogResult = true;
    }
}

public sealed record PamAccountSelectionItem(PamAccountStatus Status)
{
    public PamAccountSummary Account => Status.Account;
    public bool HasCredentials => Status.HasCredentials;
    public string RoleText => Account.IsPrimary ? "Usuario principal" : "Acceso interactivo";
    public string CredentialText => HasCredentials ? "Listo" : "Credenciales pendientes";
}
