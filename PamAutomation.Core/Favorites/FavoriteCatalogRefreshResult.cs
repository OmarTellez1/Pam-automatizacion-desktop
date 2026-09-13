namespace PamAutomation.Core.Favorites;

/// <summary>Describes one successful catalog refresh.</summary>
/// <param name="Snapshot">New complete snapshot.</param>
/// <param name="AddedCount">Identities newly present in Favorites.</param>
/// <param name="RemovedCount">Previously stored identities no longer present in Favorites.</param>
public sealed record FavoriteCatalogRefreshResult(
    FavoriteCatalogSnapshot Snapshot,
    int AddedCount,
    int RemovedCount);
