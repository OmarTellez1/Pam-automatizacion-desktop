using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PamAutomation.App.Services;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Interfaces.Credentials;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PamAutomation.App.Views;

public partial class PamAccountsWindow : Window
{
    private readonly IPamAccountService service;
    private readonly ILogger<PamAccountsWindow> logger;
    private bool busy;

    public PamAccountsWindow(IPamAccountService service, ILogger<PamAccountsWindow>? logger = null)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.logger = logger ?? NullLogger<PamAccountsWindow>.Instance;
        InitializeComponent();
        AccountsList.ItemsSource = Accounts;
        Loaded += LoadAsync;
        Closing += PreventCloseWhileBusy;
    }

    public ObservableCollection<PamAccountListItem> Accounts { get; } = [];

    private async void LoadAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= LoadAsync;
        await RefreshAsync();
    }

    private async Task RefreshAsync(PamAccountId? preferred = null)
    {
        try
        {
            IReadOnlyList<PamAccountStatus> statuses = await service.GetAccountsAsync();
            Accounts.Clear();
            foreach (PamAccountStatus status in statuses) Accounts.Add(new PamAccountListItem(status));
            AccountsList.SelectedItem = preferred is PamAccountId id ? Accounts.FirstOrDefault(item => item.Account.Id == id) : Accounts.FirstOrDefault();
            CreateSecondaryButton.IsEnabled = Accounts.Count < PamAccountRegistryState.MaximumAccounts;
            StatusText.Text = $"{Accounts.Count} de {PamAccountRegistryState.MaximumAccounts} usuarios PAM registrados.";
        }
        catch (Exception exception) { ShowFailure(exception); }
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var dialog = new PamSecondaryAccountWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            PamAccountSummary created = await service.CreateSecondaryAsync(dialog.AccountName, dialog.CredentialUpdate);
            await RefreshAsync(created.Id);
            StatusText.Text = "El usuario PAM se creó correctamente.";
        });
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (busy || GetItem(sender) is not PamAccountListItem item) return;
        var dialog = new PamAccountNameWindow(item.Account.Name, firstRun: false) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            PamAccountSummary renamed = await service.RenameAsync(item.Account.Id, dialog.AccountName);
            await RefreshAsync(renamed.Id);
            StatusText.Text = "El nombre se actualizó correctamente.";
        });
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (busy || GetItem(sender) is not PamAccountListItem item || item.Account.IsPrimary) return;
        var dialog = new PamSecondaryAccountWindow(item.Account) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            PamAccountSummary updated = await service.UpdateSecondaryAsync(item.Account.Id, dialog.AccountName, dialog.CredentialUpdate);
            await RefreshAsync(updated.Id);
            StatusText.Text = "El usuario PAM se actualizó correctamente.";
        });
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (busy || GetItem(sender) is not PamAccountListItem item || item.Account.IsPrimary) return;
        MessageBoxResult result = UrgentMessageBox.Show(
            $"¿Eliminar el usuario PAM «{item.Account.Name}»?\n\nSe eliminarán únicamente sus credenciales de acceso. Esta acción no afecta solicitudes, colecciones ni Outlook.",
            "Eliminar usuario PAM", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            await service.DeleteSecondaryAsync(item.Account.Id);
            await RefreshAsync();
            StatusText.Text = "El usuario PAM se eliminó correctamente.";
        });
    }

    private async Task RunAsync(Func<Task> operation)
    {
        busy = true;
        CreateSecondaryButton.IsEnabled = false;
        try { await operation(); }
        catch (Exception exception) { ShowFailure(exception); }
        finally { busy = false; CreateSecondaryButton.IsEnabled = Accounts.Count < PamAccountRegistryState.MaximumAccounts; }
    }

    private static PamAccountListItem? GetItem(object sender) => (sender as FrameworkElement)?.DataContext as PamAccountListItem;
    private void PreventCloseWhileBusy(object? sender, CancelEventArgs e) { if (busy) e.Cancel = true; }
    private void ShowFailure(Exception exception)
    {
        logger.LogError(exception, "PAM account administration operation failed.");
        StatusText.Text = exception is PamAccountException or CredentialStoreException
            ? exception.Message
            : "No fue posible completar la operación del usuario PAM. Intenta nuevamente.";
    }
}

public sealed record PamAccountListItem(PamAccountStatus Status)
{
    public PamAccountSummary Account => Status.Account;
    public string RoleText => Account.IsPrimary ? "Usuario principal" : "Acceso interactivo";
    public string CredentialText => Status.HasCredentials ? "Credenciales configuradas" : "Credenciales pendientes";
}
