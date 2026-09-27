using System.Windows;
using PamAutomation.App.Services;
using PamAutomation.App.ViewModels;

namespace PamAutomation.App.Views;

/// <summary>Hosts the Favorites catalog and the first step of collection management.</summary>
public partial class FavoritesCatalogWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly string smtpAddress;

    /// <summary>Initializes the collections window.</summary>
    public FavoritesCatalogWindow(FavoritesCatalogViewModel viewModel, string smtpAddress = "")
    {
        this.smtpAddress = smtpAddress;
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ((FavoritesCatalogViewModel)DataContext).InitializeAsync(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
    }

    private async void CreateCollection_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (FavoritesCatalogViewModel)DataContext;
        var editor = new FavoriteCollectionEditorWindow(
            viewModel.Favorites,
            null, smtpAddress)
        {
            Owner = this
        };
        if (editor.ShowDialog() == true && editor.Collection is not null)
        {
            await viewModel.SaveCollectionAsync(editor.Collection, lifetime.Token);
        }
    }

    private async void EditCollection_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not FavoriteCollectionItemViewModel item) return;
        var viewModel = (FavoritesCatalogViewModel)DataContext;
        var editor = new FavoriteCollectionEditorWindow(
            viewModel.Favorites,
            item.Collection, smtpAddress)
        {
            Owner = this
        };
        if (editor.ShowDialog() == true && editor.Collection is not null)
        {
            await viewModel.SaveCollectionAsync(editor.Collection, lifetime.Token);
        }
    }

    private async void ActiveCollectionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton toggle ||
            toggle.DataContext is not FavoriteCollectionItemViewModel item)
        {
            return;
        }

        toggle.IsEnabled = false;
        try
        {
            await ((FavoritesCatalogViewModel)DataContext).SetActiveCollectionAsync(
                item.Collection.Id,
                toggle.IsChecked == true,
                lifetime.Token);
        }
        finally
        {
            toggle.IsChecked = item.IsActive;
            toggle.IsEnabled = true;
        }
    }

    private async void DeleteCollection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button ||
            button.DataContext is not FavoriteCollectionItemViewModel item ||
            !item.CanDelete)
        {
            return;
        }

        MessageBoxResult confirmation = UrgentMessageBox.Show(
            $"¿Deseas eliminar la colección «{item.Name}»?\n\n" +
            "Esta acción no elimina ni modifica tus Favorites en PAM.",
            "Eliminar colección",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            await ((FavoritesCatalogViewModel)DataContext).DeleteCollectionAsync(
                item.Collection.Id,
                lifetime.Token);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ((FavoritesCatalogViewModel)DataContext).RefreshCommand.Cancel();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
