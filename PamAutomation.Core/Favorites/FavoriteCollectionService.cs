using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Execution;

namespace PamAutomation.Core.Favorites;

/// <summary>Applies collection invariants around an atomic protected store.</summary>
public sealed class FavoriteCollectionService : IFavoriteCollectionService
{
    private const string DefaultAllFavoritesReason = "Solicitud de acceso PAM";
    private readonly IFavoriteCollectionStore store;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RequestIdAccumulator? accumulator;

    /// <summary>Initializes the collection service.</summary>
    public FavoriteCollectionService(IFavoriteCollectionStore store, RequestIdAccumulator? accumulator = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.accumulator = accumulator;
    }

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> SaveCollectionAsync(
        FavoriteCollection collection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return UpdateAsync(state =>
        {
            FavoriteCollection? current = state.Collections.FirstOrDefault(existing => existing.Id == collection.Id);
            CheckAccumulation(() => accumulator?.EnsureCollectionChangeAllowed(collection.Id, deleting: false));
            if (collection.Accounts.Count == 0 && !collection.IsAllFavorites &&
                !(current?.Accounts.Count == 0 && accumulator?.CollectionId == collection.Id))
                throw new FavoriteCollectionException("Selecciona al menos una solicitud favorita para guardar la colección.");
            if (current?.IsAllFavorites == true &&
                (!collection.IsAllFavorites || !current.Accounts.SequenceEqual(collection.Accounts)))
            {
                throw new FavoriteCollectionException(
                    "All favorites solo permite editar el motivo y el correo; sus solicitudes se actualizan desde PAM.");
            }

            if (collection.IsAllFavorites && current is null)
            {
                throw new FavoriteCollectionException("All favorites solo puede crearse al obtener Favorites desde PAM.");
            }

            if (state.Collections.Any(existing =>
                    existing.Id != collection.Id &&
                    string.Equals(existing.Name, collection.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new FavoriteCollectionException("Ya existe una colección con ese nombre.");
            }

            FavoriteCollection[] updated = state.Collections
                .Where(existing => existing.Id != collection.Id)
                .Append(collection)
                .OrderByDescending(static item => item.IsAllFavorites)
                .ThenBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            return new FavoriteCollectionsState(updated, state.ActiveCollectionId);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> SetActiveCollectionAsync(
        Guid? collectionId,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(state =>
        {
            CheckAccumulation(() => accumulator?.EnsureSelectionAllowed(collectionId));
            if (collectionId is Guid id)
            {
                FavoriteCollection selected = state.Collections.FirstOrDefault(collection => collection.Id == id)
                    ?? throw new FavoriteCollectionException("La colección seleccionada ya no existe.");
                if (selected.Accounts.Count == 0)
                {
                    throw new FavoriteCollectionException("La colección seleccionada no contiene solicitudes disponibles.");
                }
            }

            return new FavoriteCollectionsState(state.Collections, collectionId);
        }, cancellationToken);

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> DeleteCollectionAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        if (collectionId == Guid.Empty)
        {
            throw new FavoriteCollectionException("La colección que deseas eliminar no es válida.");
        }

        return UpdateAsync(state =>
        {
            CheckAccumulation(() => accumulator?.EnsureCollectionChangeAllowed(collectionId, deleting: true));
            FavoriteCollection collection = state.Collections
                .FirstOrDefault(existing => existing.Id == collectionId)
                ?? throw new FavoriteCollectionException("La colección que deseas eliminar ya no existe.");
            if (collection.IsAllFavorites)
            {
                throw new FavoriteCollectionException("All favorites es una colección automática y no se puede eliminar.");
            }

            FavoriteCollection[] remaining = state.Collections
                .Where(existing => existing.Id != collectionId)
                .ToArray();
            Guid? activeCollectionId = state.ActiveCollectionId == collectionId
                ? remaining.FirstOrDefault(static existing => existing.IsAllFavorites)?.Id
                : state.ActiveCollectionId;
            return new FavoriteCollectionsState(remaining, activeCollectionId);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FavoriteCollectionsState> ReconcileAsync(
        FavoriteCatalogSnapshot catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        FavoriteAccountKey[] allFavoriteKeys = catalog.Entries.Select(FavoriteAccountKey.From).ToArray();
        HashSet<FavoriteAccountKey> available = allFavoriteKeys.ToHashSet();
        return UpdateAsync(state =>
        {
            FavoriteCollection? allFavorites = state.Collections.FirstOrDefault(static collection => collection.IsAllFavorites);
            FavoriteCollection synchronizedAll = new(
                allFavorites?.Id ?? Guid.NewGuid(),
                FavoriteCollection.AllFavoritesName,
                allFavorites?.RequestReason ?? DefaultAllFavoritesReason,
                allFavoriteKeys,
                isAllFavorites: true,
                mail: allFavorites?.Mail);

            FavoriteCollection[] custom = state.Collections
                .Where(static collection => !collection.IsAllFavorites)
                .Select(collection => new FavoriteCollection(
                    collection.Id,
                    collection.Name,
                    collection.RequestReason,
                    collection.Accounts.Where(available.Contains).ToArray(),
                    mail: collection.Mail))
                .ToArray();
            FavoriteCollection[] reconciled = [synchronizedAll, .. custom];

            Guid? retainedActiveId = state.ActiveCollectionId;
            FavoriteCollection? active = retainedActiveId is Guid activeId
                ? reconciled.FirstOrDefault(collection => collection.Id == activeId)
                : null;
            if (accumulator?.CollectionId is Guid lockedId)
            {
                CheckAccumulation(() => accumulator.EnsureCollectionChangeAllowed(lockedId, deleting: false));
                retainedActiveId = lockedId;
            }
            else if (active is null || active.Accounts.Count == 0)
            {
                retainedActiveId = synchronizedAll.Id;
            }

            return new FavoriteCollectionsState(reconciled, retainedActiveId);
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
            CheckAccumulation(() => accumulator?.EnsureSelectionAllowed(updated.ActiveCollectionId));
            await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    private static void CheckAccumulation(Action check)
    {
        try { check(); }
        catch (InvalidOperationException exception) { throw new FavoriteCollectionException(exception.Message); }
    }
}
