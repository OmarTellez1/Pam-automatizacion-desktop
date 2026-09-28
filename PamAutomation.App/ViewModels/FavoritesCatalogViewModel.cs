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
    private readonly IFavoriteCollectionService? collectionService;
    private readonly ILogger<FavoritesCatalogViewModel> logger;
    private bool initialized;
    private readonly PamAutomation.Core.Execution.RequestIdAccumulator? accumulator;
    public Guid? AccumulatedCollectionId => accumulator?.CollectionId;
    public string AccumulationNotice => accumulator?.Count > 0
        ? PamAutomation.Core.Execution.RequestIdAccumulator.CollectionLockMessage + " Si la colección queda vacía, podrás enviar sus IDs, pero no iniciar nuevas solicitudes."
        : string.Empty;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCollections))]
    private int collectionCount;

    /// <summary>Initializes the Favorites catalog view model.</summary>
    public FavoritesCatalogViewModel(
        IFavoriteCatalogService catalogService,
        IFavoriteCollectionService? collectionService = null,
        ILogger<FavoritesCatalogViewModel>? logger = null,
        PamAutomation.Core.Execution.RequestIdAccumulator? accumulator = null)
    {
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.collectionService = collectionService;
        this.accumulator = accumulator;
        this.logger = logger ?? NullLogger<FavoritesCatalogViewModel>.Instance;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => CanRefresh);
    }

    /// <summary>Gets the current catalog rows.</summary>
    public ObservableCollection<FavoriteCatalogEntry> Favorites { get; } = [];

    /// <summary>Gets every saved collection.</summary>
    public ObservableCollection<FavoriteCollectionItemViewModel> Collections { get; } = [];

    /// <summary>Gets the command that obtains or refreshes all Favorites pages.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Gets whether the refresh action can currently run.</summary>
    public bool CanRefresh => !IsLoading;

    /// <summary>Gets whether the first-use empty state should be shown.</summary>
    public bool IsEmpty => !HasFavorites;

    /// <summary>Gets whether at least one collection exists.</summary>
    public bool HasCollections => CollectionCount > 0;

    /// <summary>Gets whether a collection can be created from the current catalog.</summary>
    public bool CanCreateCollection => HasFavorites && !IsLoading && collectionService is not null;

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
            int removedSelections = await LoadAndReconcileCollectionsAsync(snapshot, cancellationToken);
            StatusMessage = removedSelections > 0
                ? $"{Favorites.Count} solicitudes favoritas guardadas. Se retiraron {removedSelections} selecciones que ya no existen."
                : $"{Favorites.Count} solicitudes favoritas guardadas. Pulsa Actualizar para consultar nuevamente PAM.";
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
            int removedSelections = await LoadAndReconcileCollectionsAsync(
                result.Snapshot,
                cancellationToken);
            StatusMessage = wasEmpty
                ? $"Se obtuvieron {Favorites.Count} solicitudes favoritas y se creó All favorites."
                : $"Se actualizaron {Favorites.Count} solicitudes favoritas. Nuevas: {result.AddedCount}. Eliminadas: {result.RemovedCount}.";
            if (removedSelections > 0)
            {
                StatusMessage += $" Se retiraron {removedSelections} selecciones de las colecciones.";
            }
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
        OnPropertyChanged(nameof(CanCreateCollection));
    }

    /// <summary>Creates or updates one collection and refreshes its presentation.</summary>
    public async Task SaveCollectionAsync(
        FavoriteCollection collection,
        CancellationToken cancellationToken = default)
    {
        if (collectionService is null) throw new InvalidOperationException("El servicio de colecciones no está disponible.");
        try
        {
            FavoriteCollectionsState state = await collectionService
                .SaveCollectionAsync(collection, cancellationToken);
            ReplaceCollections(state);
            HasError = false;
            StatusMessage = $"La colección «{collection.Name}» se guardó correctamente.";
        }
        catch (FavoriteCollectionException exception)
        {
            HasError = true;
            StatusMessage = exception.Message;
            logger.LogWarning(exception, "Favorite collection save failed safely.");
        }
        catch (Exception exception)
        {
            HasError = true;
            StatusMessage = "No fue posible guardar la colección. Exporta el diagnóstico si el problema continúa.";
            logger.LogError(exception, "Favorite collection save failed unexpectedly.");
        }
    }

    /// <summary>Selects or clears the collection used when collection automation is enabled.</summary>
    public async Task SetActiveCollectionAsync(
        Guid collectionId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (collectionService is null) throw new InvalidOperationException("El servicio de colecciones no está disponible.");
        try
        {
            FavoriteCollectionsState state = await collectionService
                .SetActiveCollectionAsync(isActive ? collectionId : null, cancellationToken);
            ReplaceCollections(state);
            HasError = false;
            StatusMessage = isActive
                ? "Colección seleccionada para la automatización."
                : "Ya no hay una colección seleccionada para la automatización.";
        }
        catch (FavoriteCollectionException exception)
        {
            HasError = true;
            StatusMessage = exception.Message;
            logger.LogWarning(exception, "Favorite collection activation failed safely.");
        }
        catch (Exception exception)
        {
            HasError = true;
            StatusMessage = "No fue posible cambiar la colección activa. Exporta el diagnóstico si el problema continúa.";
            logger.LogError(exception, "Favorite collection activation failed unexpectedly.");
        }
    }

    /// <summary>Deletes one user-created collection and refreshes the protected state.</summary>
    public async Task DeleteCollectionAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        if (collectionService is null) throw new InvalidOperationException("El servicio de colecciones no está disponible.");
        try
        {
            FavoriteCollectionsState state = await collectionService
                .DeleteCollectionAsync(collectionId, cancellationToken);
            ReplaceCollections(state);
            HasError = false;
            StatusMessage = "La colección se eliminó correctamente.";
            logger.LogInformation("User-created favorite collection deleted. CollectionId={CollectionId}.", collectionId);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La eliminación de la colección fue cancelada.";
            logger.LogInformation("Favorite collection deletion was cancelled. CollectionId={CollectionId}.", collectionId);
        }
        catch (FavoriteCollectionException exception)
        {
            HasError = true;
            StatusMessage = exception.Message;
            logger.LogWarning(exception, "Favorite collection deletion failed safely. CollectionId={CollectionId}.", collectionId);
        }
        catch (Exception exception)
        {
            HasError = true;
            StatusMessage = "No fue posible eliminar la colección. Exporta el diagnóstico si el problema continúa.";
            logger.LogError(exception, "Favorite collection deletion failed unexpectedly. CollectionId={CollectionId}.", collectionId);
        }
    }

    private async Task<int> LoadAndReconcileCollectionsAsync(
        FavoriteCatalogSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (collectionService is null) return 0;
        FavoriteCollectionsState before = await collectionService.LoadAsync(cancellationToken);
        FavoriteCollectionsState state = await collectionService.ReconcileAsync(snapshot, cancellationToken);
        int removed = before.Collections.Sum(collection => collection.Accounts.Count) -
            state.Collections.Sum(collection => collection.Accounts.Count);
        ReplaceCollections(state);
        return removed;
    }

    private void ReplaceCollections(FavoriteCollectionsState state)
    {
        Collections.Clear();
        foreach (FavoriteCollection collection in state.Collections)
        {
            Collections.Add(new FavoriteCollectionItemViewModel(
                collection,
                state.ActiveCollectionId == collection.Id,
                accumulator?.CollectionId));
        }

        CollectionCount = Collections.Count;
        OnPropertyChanged(nameof(AccumulationNotice));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanCreateCollection));
    }
}
