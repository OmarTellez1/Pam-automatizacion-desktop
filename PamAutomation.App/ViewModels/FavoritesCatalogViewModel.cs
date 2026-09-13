using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.App.ViewModels;

/// <summary>Provides the phase 20.1 Favorites catalog presentation and refresh command.</summary>
public sealed partial class FavoritesCatalogViewModel : ObservableObject
{
    private readonly IFavoriteCatalogService catalogService;
    private readonly ILogger<FavoritesCatalogViewModel> logger;
    private bool initialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RefreshButtonText))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool hasFavorites;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "Aún no has obtenido tus solicitudes favoritas desde PAM.";

    [ObservableProperty]
    private bool hasError;

    /// <summary>Initializes the Favorites catalog view model.</summary>
    public FavoritesCatalogViewModel(
        IFavoriteCatalogService catalogService,
        ILogger<FavoritesCatalogViewModel>? logger = null)
    {
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.logger = logger ?? NullLogger<FavoritesCatalogViewModel>.Instance;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => CanRefresh);
    }

    /// <summary>Gets the current catalog rows.</summary>
    public ObservableCollection<FavoriteCatalogEntry> Favorites { get; } = [];

    /// <summary>Gets the command that obtains or refreshes all Favorites pages.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Gets whether the refresh action can currently run.</summary>
    public bool CanRefresh => !IsLoading;

    /// <summary>Gets whether the first-use empty state should be shown.</summary>
    public bool IsEmpty => !HasFavorites;

    /// <summary>Gets the action label according to whether a protected snapshot exists.</summary>
    public string RefreshButtonText => HasFavorites
        ? "Actualizar solicitudes favoritas"
        : "Obtener solicitudes favoritas";

    /// <summary>Loads the last protected snapshot without contacting PAM.</summary>
    /// <param name="cancellationToken">Cancels the local read.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized) return;
        initialized = true;

        try
        {
            FavoriteCatalogSnapshot? snapshot = await catalogService.LoadAsync(cancellationToken);
            if (snapshot is null) return;

            ReplaceFavorites(snapshot);
            StatusMessage = $"{Favorites.Count} solicitudes favoritas guardadas. Pulsa Actualizar para consultar nuevamente PAM.";
            logger.LogInformation("Protected Favorites catalog loaded. FavoriteCount={FavoriteCount}.", Favorites.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            HasError = true;
            StatusMessage = "No fue posible leer el catálogo guardado. Puedes intentar obtener nuevamente las solicitudes favoritas.";
            logger.LogError(exception, "Protected Favorites catalog load failed.");
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Conectando con PAM y recorriendo todas las páginas de Favorites…";
        logger.LogInformation("Favorites catalog refresh started.");
        try
        {
            bool wasEmpty = !HasFavorites;
            FavoriteCatalogRefreshResult result = await catalogService.RefreshAsync(cancellationToken);
            ReplaceFavorites(result.Snapshot);
            StatusMessage = wasEmpty
                ? $"Se obtuvieron {Favorites.Count} solicitudes favoritas."
                : $"Se actualizaron {Favorites.Count} solicitudes favoritas. Nuevas: {result.AddedCount}. Eliminadas: {result.RemovedCount}.";
            logger.LogInformation(
                "Favorites catalog refresh completed. FavoriteCount={FavoriteCount}, AddedCount={AddedCount}, RemovedCount={RemovedCount}.",
                Favorites.Count,
                result.AddedCount,
                result.RemovedCount);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La actualización de solicitudes favoritas fue cancelada.";
            logger.LogInformation("Favorites catalog refresh was cancelled.");
        }
        catch (FavoriteCatalogException exception)
        {
            HasError = true;
            StatusMessage = exception.Message;
            logger.LogWarning(exception, "Favorites catalog refresh failed safely.");
        }
        catch (Exception exception)
        {
            HasError = true;
            StatusMessage = "No fue posible actualizar las solicitudes favoritas. Comprueba la conexión e inténtalo nuevamente.";
            logger.LogError(exception, "Favorites catalog refresh failed unexpectedly.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ReplaceFavorites(FavoriteCatalogSnapshot snapshot)
    {
        Favorites.Clear();
        foreach (FavoriteCatalogEntry entry in snapshot.Entries)
        {
            Favorites.Add(entry);
        }

        HasFavorites = Favorites.Count > 0;
    }

    partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();
}
