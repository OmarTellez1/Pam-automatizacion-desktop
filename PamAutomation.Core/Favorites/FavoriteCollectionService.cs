using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.Core.Favorites;

/// <summary>Applies collection invariants around an atomic protected store.</summary>
public sealed class FavoriteCollectionService : IFavoriteCollectionService
{
    private readonly IFavoriteCollectionStore store;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Initializes the collection service.</summary>
    public FavoriteCollectionService(IFavoriteCollectionStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> SaveCollectionAsync(
        FavoriteCollection collection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (collection.Accounts.Count == 0)
        {
            throw new FavoriteCollectionException("Selecciona al menos una solicitud favorita para guardar la colección.");
        }

        return UpdateAsync(state =>
        {
            if (state.Collections.Any(existing =>
                    existing.Id != collection.Id &&
                    string.Equals(existing.Name, collection.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new FavoriteCollectionException("Ya existe una colección con ese nombre.");
            }

            FavoriteCollection[] updated = state.Collections
                .Where(existing => existing.Id != collection.Id)
                .Append(collection)
                .OrderBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            return new FavoriteCollectionsState(updated, state.ActiveCollectionId, state.UseCollectionsForAutomation);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> SetActiveCollectionAsync(
        Guid? collectionId,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(state =>
        {
            if (collectionId is Guid id)
            {
                FavoriteCollection selected = state.Collections.FirstOrDefault(collection => collection.Id == id)
                    ?? throw new FavoriteCollectionException("La colección seleccionada ya no existe.");
                if (selected.Accounts.Count == 0)
                {
                    throw new FavoriteCollectionException("La colección seleccionada no contiene solicitudes disponibles.");
                }
            }

            bool useCollections = collectionId is null ? false : state.UseCollectionsForAutomation;
            return new FavoriteCollectionsState(state.Collections, collectionId, useCollections);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> SetAutomationModeAsync(
        bool useCollections,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(state =>
        {
            if (useCollections && state.ActiveCollection is null)
            {
                throw new FavoriteCollectionException(
                    "Selecciona primero una colección dentro de Colecciones para usarla en la automatización.");
            }

            return new FavoriteCollectionsState(state.Collections, state.ActiveCollectionId, useCollections);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> ReconcileAsync(
        FavoriteCatalogSnapshot catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        HashSet<FavoriteAccountKey> available = catalog.Entries
            .Select(FavoriteAccountKey.From)
            .ToHashSet();
        return UpdateAsync(state =>
        {
            FavoriteCollection[] reconciled = state.Collections.Select(collection => new FavoriteCollection(
                collection.Id,
                collection.Name,
                collection.Request,
                collection.Accounts.Where(available.Contains).ToArray())).ToArray();
            FavoriteCollection? active = state.ActiveCollectionId is Guid activeId
                ? reconciled.First(collection => collection.Id == activeId)
                : null;
            Guid? retainedActiveId = active is { Accounts.Count: > 0 } ? active.Id : null;
            bool useCollections = state.UseCollectionsForAutomation && retainedActiveId is not null;
            return new FavoriteCollectionsState(reconciled, retainedActiveId, useCollections);
        }, cancellationToken);
    }

    private async Task<FavoriteCollectionsState> UpdateAsync(
        Func<FavoriteCollectionsState, FavoriteCollectionsState> update,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            FavoriteCollectionsState current = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            FavoriteCollectionsState updated = update(current);
            await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }
}
