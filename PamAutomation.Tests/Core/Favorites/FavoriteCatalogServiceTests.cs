using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Tests.Core.Favorites;

[TestClass]
public sealed class FavoriteCatalogServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task RefreshAsync_FirstSnapshot_PersistsEveryFavoriteAndReportsAllAsAdded()
    {
        var store = new MemoryCatalogStore();
        var reader = new StubFavoritesReader(
        [
            new("system-a", "NOSQLBOOSTER", "account-a", "10.31.32.25"),
            new("system-b", "PLSQL - Nodo1", "account-b", "10.226.4.64")
        ]);
        FavoriteCatalogService service = CreateService(reader, store);

        FavoriteCatalogRefreshResult result = await service.RefreshAsync();

        Assert.AreEqual(2, result.Snapshot.Entries.Count);
        Assert.AreEqual(2, result.AddedCount);
        Assert.AreEqual(0, result.RemovedCount);
        Assert.AreSame(result.Snapshot, store.Snapshot);
        Assert.AreEqual("NOSQLBOOSTER - 10.31.32.25", result.Snapshot.Entries[0].DisplayName);
    }

    [TestMethod]
    public async Task RefreshAsync_ChangedSnapshot_ReportsAddedAndRemovedIdentities()
    {
        var store = new MemoryCatalogStore
        {
            Snapshot = CreateSnapshot(
                new FavoriteCatalogEntry("system-a", "app-a", "account-a", "10.0.0.1"),
                new FavoriteCatalogEntry("system-b", "app-b", "account-b", "10.0.0.2"))
        };
        var reader = new StubFavoritesReader(
        [
            new("system-b", "app-b", "account-b", "10.0.0.22"),
            new("system-c", "app-c", "account-c", "10.0.0.3")
        ]);

        FavoriteCatalogRefreshResult result = await CreateService(reader, store).RefreshAsync();

        Assert.AreEqual(1, result.AddedCount);
        Assert.AreEqual(1, result.RemovedCount);
        Assert.HasCount(2, result.Snapshot.Entries);
        Assert.AreEqual("10.0.0.22", result.Snapshot.Entries[0].IpAddress);
    }

    [TestMethod]
    public async Task RefreshAsync_MissingCredentials_DoesNotContactPamOrOverwriteSnapshot()
    {
        var reader = new StubFavoritesReader([]);
        var store = new MemoryCatalogStore
        {
            Snapshot = CreateSnapshot(new FavoriteCatalogEntry("s", "a", "c", "10.0.0.1"))
        };
        var service = new FavoriteCatalogService(
            reader,
            new StubCredentialStore(null),
            store,
            new FixedTimeProvider(Now));

        FavoriteCatalogException exception = await Assert.ThrowsExactlyAsync<FavoriteCatalogException>(
            () => service.RefreshAsync());

        Assert.Contains("credenciales PAM", exception.Message);
        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    public async Task RefreshAsync_FavoriteWithoutIpAddress_DoesNotReplaceValidSnapshot()
    {
        var store = new MemoryCatalogStore
        {
            Snapshot = CreateSnapshot(new FavoriteCatalogEntry("s", "a", "c", "10.0.0.1"))
        };
        var reader = new StubFavoritesReader([new PamFavoriteAccount("s", "a", "c")]);

        FavoriteCatalogException exception = await Assert.ThrowsExactlyAsync<FavoriteCatalogException>(
            () => CreateService(reader, store).RefreshAsync());

        Assert.Contains("IP Address", exception.Message);
        Assert.AreEqual(0, store.SaveCount);
        Assert.AreEqual("10.0.0.1", store.Snapshot!.Entries[0].IpAddress);
    }

    private static FavoriteCatalogService CreateService(
        IPamFavoritesReader reader,
        IFavoriteCatalogStore store) => new(
            reader,
            new StubCredentialStore(new PamCredentials("user", "password", "AAAAAAAAAAAAAAAA")),
            store,
            new FixedTimeProvider(Now));

    private static FavoriteCatalogSnapshot CreateSnapshot(params FavoriteCatalogEntry[] entries) => new(Now, entries);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubFavoritesReader(IReadOnlyList<PamFavoriteAccount> favorites) : IPamFavoritesReader
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(favorites);
        }
    }

    private sealed class StubCredentialStore(PamCredentials? credentials) : IPamCredentialStore
    {
        public bool HasCredentials() => credentials is not null;
        public PamCredentials? LoadForAuthentication() => credentials;
        public void Save(PamCredentialUpdate update) => throw new NotSupportedException();
        public void Delete() => throw new NotSupportedException();
    }

    private sealed class MemoryCatalogStore : IFavoriteCatalogStore
    {
        public FavoriteCatalogSnapshot? Snapshot { get; set; }
        public int SaveCount { get; private set; }
        public bool Exists() => Snapshot is not null;
        public Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);

        public Task SaveAsync(FavoriteCatalogSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Snapshot = snapshot;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
