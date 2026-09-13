using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Models.Configuration;

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
    public async Task SetAutomationModeAsync_NoActiveCollection_RejectsEnable()
    {
        var service = new FavoriteCollectionService(new MemoryStore());

        FavoriteCollectionException exception = await Assert.ThrowsExactlyAsync<FavoriteCollectionException>(
            () => service.SetAutomationModeAsync(true));

        Assert.Contains("Selecciona primero", exception.Message);
    }

    [TestMethod]
    public async Task ReconcileAsync_RemovedFavorite_RemovesSelectionAndDisablesEmptyActiveCollection()
    {
        var store = new MemoryStore();
        var service = new FavoriteCollectionService(store);
        FavoriteCollection collection = CreateCollection("Accesos", "removed-system");
        await service.SaveCollectionAsync(collection);
        await service.SetActiveCollectionAsync(collection.Id);
        await service.SetAutomationModeAsync(true);
        var catalog = new FavoriteCatalogSnapshot(
            DateTimeOffset.UtcNow,
            [new FavoriteCatalogEntry("other-system", "app", "account", "10.0.0.2")]);

        FavoriteCollectionsState state = await service.ReconcileAsync(catalog);

        Assert.IsNull(state.ActiveCollectionId);
        Assert.IsFalse(state.UseCollectionsForAutomation);
        Assert.IsEmpty(state.Collections.Single().Accounts);
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

    private static FavoriteCollection CreateCollection(string name, string system) => new(
        Guid.NewGuid(),
        name,
        new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 14),
            StartTime = new TimeOnly(9, 0),
            SessionHours = 1,
            RequestReason = "Prueba"
        },
        [new FavoriteAccountKey(system, "app", "account")]);

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
