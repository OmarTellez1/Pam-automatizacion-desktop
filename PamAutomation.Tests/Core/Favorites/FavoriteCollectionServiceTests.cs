using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.Tests.Core.Favorites;

[TestClass]
public sealed class FavoriteCollectionServiceTests
{
    [TestMethod]
    public async Task SaveAndActivateAsync_MultipleCollections_KeepsOneActive()
    {
        var store = new MemoryStore();
        var service = new FavoriteCollectionService(store);
        FavoriteCollection first = CreateCollection("Primera", "system-1");
        FavoriteCollection second = CreateCollection("Segunda", "system-2");

        await service.SaveCollectionAsync(first);
        await service.SaveCollectionAsync(second);
        await service.SetActiveCollectionAsync(first.Id);
        FavoriteCollectionsState state = await service.SetActiveCollectionAsync(second.Id);

        Assert.AreEqual(second.Id, state.ActiveCollectionId);
        Assert.HasCount(2, state.Collections);
    }

    [TestMethod]
    public async Task ReconcileAsync_FirstFavoritesRead_CreatesAndActivatesAllFavorites()
    {
        var service = new FavoriteCollectionService(new MemoryStore());

        FavoriteCollectionsState state = await service.ReconcileAsync(CreateCatalog("system-1", "system-2"));

        Assert.IsNotNull(state.ActiveCollection);
        FavoriteCollection all = state.ActiveCollection;
        Assert.IsTrue(all.IsAllFavorites);
        Assert.AreEqual(FavoriteCollection.AllFavoritesName, all.Name);
        Assert.HasCount(2, all.Accounts);
    }

    [TestMethod]
    public async Task ReconcileAsync_ChangedCatalog_SynchronizesAllFavoritesAndPrunesCustomCollection()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        await service.ReconcileAsync(CreateCatalog("system-1", "system-2"));
        FavoriteCollection custom = CreateCollection("Accesos", "system-2");
        await service.SaveCollectionAsync(custom);
        await service.SetActiveCollectionAsync(custom.Id);

        FavoriteCollectionsState state = await service.ReconcileAsync(CreateCatalog("system-1", "system-3"));

        FavoriteCollection all = state.Collections.Single(static collection => collection.IsAllFavorites);
        Assert.HasCount(2, all.Accounts);
        Assert.IsTrue(all.Accounts.Any(key => key.SystemName == "system-3"));
        Assert.IsEmpty(state.Collections.Single(collection => collection.Id == custom.Id).Accounts);
        Assert.AreEqual(all.Id, state.ActiveCollectionId);
    }

    [TestMethod]
    public async Task SaveCollectionAsync_AllFavoritesAccountEdit_IsRejected()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        FavoriteCollectionsState state = await service.ReconcileAsync(CreateCatalog("system-1", "system-2"));
        FavoriteCollection all = state.ActiveCollection!;
        var changed = new FavoriteCollection(
            all.Id,
            all.Name,
            "Motivo actualizado",
            [all.Accounts[0]],
            isAllFavorites: true);

        FavoriteCollectionException exception = await Assert.ThrowsExactlyAsync<FavoriteCollectionException>(
            () => service.SaveCollectionAsync(changed));

        Assert.Contains("solo permite editar el motivo", exception.Message);
    }

    [TestMethod]
    public async Task SaveCollectionAsync_AllFavoritesReasonEdit_IsAllowed()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        FavoriteCollectionsState state = await service.ReconcileAsync(CreateCatalog("system-1", "system-2"));
        FavoriteCollection all = state.ActiveCollection!;
        var changed = new FavoriteCollection(
            all.Id,
            all.Name,
            "Motivo actualizado",
            all.Accounts,
            isAllFavorites: true);

        FavoriteCollectionsState saved = await service.SaveCollectionAsync(changed);

        Assert.AreEqual("Motivo actualizado", saved.ActiveCollection?.RequestReason);
        Assert.HasCount(2, saved.ActiveCollection!.Accounts);
    }

    [TestMethod]
    public async Task SaveCollectionAsync_DuplicateName_RejectsAmbiguousPresentation()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        await service.SaveCollectionAsync(CreateCollection("Producción", "system-1"));

        FavoriteCollectionException exception = await Assert.ThrowsExactlyAsync<FavoriteCollectionException>(
            () => service.SaveCollectionAsync(CreateCollection("producción", "system-2")));

        Assert.Contains("ese nombre", exception.Message);
    }

    [TestMethod]
    public async Task DeleteCollectionAsync_InactiveCustom_RemovesItAndPreservesActiveCollection()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        FavoriteCollectionsState initial = await service.ReconcileAsync(CreateCatalog("system-1"));
        FavoriteCollection custom = CreateCollection("Operaciones", "system-1");
        await service.SaveCollectionAsync(custom);

        FavoriteCollectionsState state = await service.DeleteCollectionAsync(custom.Id);

        Assert.HasCount(1, state.Collections);
        Assert.IsTrue(state.Collections[0].IsAllFavorites);
        Assert.AreEqual(initial.ActiveCollectionId, state.ActiveCollectionId);
    }

    [TestMethod]
    public async Task DeleteCollectionAsync_ActiveCustom_FallsBackToAllFavorites()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        FavoriteCollectionsState initial = await service.ReconcileAsync(CreateCatalog("system-1"));
        FavoriteCollection custom = CreateCollection("Operaciones", "system-1");
        await service.SaveCollectionAsync(custom);
        await service.SetActiveCollectionAsync(custom.Id);

        FavoriteCollectionsState state = await service.DeleteCollectionAsync(custom.Id);

        Assert.AreEqual(initial.ActiveCollectionId, state.ActiveCollectionId);
        Assert.IsTrue(state.ActiveCollection!.IsAllFavorites);
        Assert.DoesNotContain(custom, state.Collections);
    }

    [TestMethod]
    public async Task DeleteCollectionAsync_AllFavorites_IsRejected()
    {
        var service = new FavoriteCollectionService(new MemoryStore());
        FavoriteCollectionsState initial = await service.ReconcileAsync(CreateCatalog("system-1"));

        FavoriteCollectionException exception = await Assert.ThrowsExactlyAsync<FavoriteCollectionException>(
            () => service.DeleteCollectionAsync(initial.ActiveCollectionId!.Value));

        Assert.Contains("no se puede eliminar", exception.Message);
        FavoriteCollectionsState preserved = await service.LoadAsync();
        Assert.AreEqual(initial, preserved);
    }

    private static FavoriteCollection CreateCollection(string name, string system) => new(
        Guid.NewGuid(),
        name,
        "Prueba",
        [new FavoriteAccountKey(system, "app", $"account-{system}")]);

    private static FavoriteCatalogSnapshot CreateCatalog(params string[] systems) => new(
        DateTimeOffset.UtcNow,
        systems.Select(system => new FavoriteCatalogEntry(system, "app", $"account-{system}", "10.0.0.1")).ToArray());

    private sealed class MemoryStore : IFavoriteCollectionStore
    {
        private FavoriteCollectionsState state = new();
        public Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(state);
        public Task SaveAsync(FavoriteCollectionsState state, CancellationToken cancellationToken = default)
        {
            this.state = state;
            return Task.CompletedTask;
        }
    }
}
