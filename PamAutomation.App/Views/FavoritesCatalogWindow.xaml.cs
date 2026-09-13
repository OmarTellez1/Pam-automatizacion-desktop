using System.Windows;
using PamAutomation.App.ViewModels;

namespace PamAutomation.App.Views;

/// <summary>Hosts the Favorites catalog and the first step of collection management.</summary>
public partial class FavoritesCatalogWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();

    /// <summary>Initializes the collections window.</summary>
    public FavoritesCatalogWindow(FavoritesCatalogViewModel viewModel)
    {
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

    private void Window_Closed(object? sender, EventArgs e)
    {
        ((FavoritesCatalogViewModel)DataContext).RefreshCommand.Cancel();
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
