using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Views;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

public sealed class WpfConfigurationDialogService : IConfigurationDialogService
{
    private readonly ICredentialConfigurationWorkflow credentialWorkflow;
    private readonly ILogger<CredentialsWindow> credentialsWindowLogger;
    private readonly ILogger<MailSettingsWindow> mailSettingsWindowLogger;
    private readonly IFavoriteCatalogService? favoriteCatalogService;
    private readonly IFavoriteCollectionService? favoriteCollectionService;
    private readonly ILogger<FavoritesCatalogViewModel> favoritesCatalogLogger;
    private readonly IPamAccountService? pamAccountService;
    private readonly ILogger<PamAccountsWindow> pamAccountsWindowLogger;
    private readonly PamAutomation.Core.Execution.RequestIdAccumulator? accumulator;

    public WpfConfigurationDialogService(
        ICredentialConfigurationWorkflow credentialWorkflow,
        ILogger<CredentialsWindow>? credentialsWindowLogger = null,
        ILogger<MailSettingsWindow>? mailSettingsWindowLogger = null,
        IFavoriteCatalogService? favoriteCatalogService = null,
        IFavoriteCollectionService? favoriteCollectionService = null,
        ILogger<FavoritesCatalogViewModel>? favoritesCatalogLogger = null,
        IPamAccountService? pamAccountService = null,
        ILogger<PamAccountsWindow>? pamAccountsWindowLogger = null,
        PamAutomation.Core.Execution.RequestIdAccumulator? accumulator = null)
    {
        this.credentialWorkflow = credentialWorkflow ?? throw new ArgumentNullException(nameof(credentialWorkflow));
        this.credentialsWindowLogger = credentialsWindowLogger ?? NullLogger<CredentialsWindow>.Instance;
        this.mailSettingsWindowLogger = mailSettingsWindowLogger ?? NullLogger<MailSettingsWindow>.Instance;
        this.favoriteCatalogService = favoriteCatalogService;
        this.favoriteCollectionService = favoriteCollectionService;
        this.favoritesCatalogLogger = favoritesCatalogLogger ?? NullLogger<FavoritesCatalogViewModel>.Instance;
        this.pamAccountService = pamAccountService;
        this.pamAccountsWindowLogger = pamAccountsWindowLogger ?? NullLogger<PamAccountsWindow>.Instance;
        this.accumulator = accumulator;
    }

    public Task<CredentialConfigurationState> EditCredentialsAsync(
        CredentialConfigurationState current,
        CancellationToken cancellationToken = default)
    {
        var dialog = new CredentialsWindow(current, credentialWorkflow, cancellationToken, credentialsWindowLogger)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
        return Task.FromResult(dialog.CurrentState);
    }

    public RequestConfigurationEdit? EditRequest(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday)
    {
        var dialog = new RequestSettingsWindow(current, testModeEnabled, ecuadorToday) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.Edit : null;
    }

    public MailSignature? EditSignature(MailSignature? current)
    {
        var dialog = new MailSettingsWindow(current, mailSettingsWindowLogger)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Signature : null;
    }

    public void OpenFavoriteCollections(string smtpAddress)
    {
        if (favoriteCatalogService is null)
        {
            throw new InvalidOperationException("El catálogo de solicitudes favoritas no está disponible.");
        }

        var viewModel = new FavoritesCatalogViewModel(
            favoriteCatalogService,
            favoriteCollectionService,
            favoritesCatalogLogger, accumulator);
        var dialog = new FavoritesCatalogWindow(viewModel, smtpAddress) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    public void OpenPamAccounts()
    {
        if (pamAccountService is null) throw new InvalidOperationException("La administración de usuarios PAM no está disponible.");
        var dialog = new PamAccountsWindow(pamAccountService, pamAccountsWindowLogger) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    public void OpenAbout()
    {
        var dialog = new AboutWindow { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    public PamAccountId? SelectPamAccount(IReadOnlyList<PamAccountStatus> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var dialog = new PamAccountSelectionWindow(accounts) { Owner = Application.Current.MainWindow };
        return dialog.ShowDialog() == true ? dialog.SelectedAccountId : null;
    }
}
