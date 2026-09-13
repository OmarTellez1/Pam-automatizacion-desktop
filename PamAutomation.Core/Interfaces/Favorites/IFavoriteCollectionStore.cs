using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Persists the protected collection state for the current Windows user.</summary>
public interface IFavoriteCollectionStore
{
    /// <summary>Loads the current state or an empty state when none exists.</summary>
    Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the protected state.</summary>
    Task SaveAsync(FavoriteCollectionsState state, CancellationToken cancellationToken = default);
}
