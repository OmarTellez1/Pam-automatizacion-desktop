using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Coordinates collection editing, activation and catalog reconciliation.</summary>
public interface IFavoriteCollectionService
{
    /// <summary>Loads all collections and their automation selection.</summary>
    Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces one collection.</summary>
    Task<FavoriteCollectionsState> SaveCollectionAsync(
        FavoriteCollection collection,
        CancellationToken cancellationToken = default);

    /// <summary>Selects one collection for automation, or clears the selection.</summary>
    Task<FavoriteCollectionsState> SetActiveCollectionAsync(
        Guid? collectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Synchronizes All favorites and removes missing keys from custom collections.</summary>
    Task<FavoriteCollectionsState> ReconcileAsync(
        FavoriteCatalogSnapshot catalog,
        CancellationToken cancellationToken = default);
}
