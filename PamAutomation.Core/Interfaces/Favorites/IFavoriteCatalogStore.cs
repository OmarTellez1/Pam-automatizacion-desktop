using PamAutomation.Core.Favorites;

namespace PamAutomation.Core.Interfaces.Favorites;

/// <summary>Persists the private, per-user snapshot used by the collections area.</summary>
public interface IFavoriteCatalogStore
{
    /// <summary>Returns whether a protected snapshot currently exists.</summary>
    bool Exists();

    /// <summary>Loads the last protected snapshot, or <see langword="null"/> before the first synchronization.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the protected snapshot.</summary>
    /// <param name="snapshot">Validated snapshot to persist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task SaveAsync(FavoriteCatalogSnapshot snapshot, CancellationToken cancellationToken = default);
}
