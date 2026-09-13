using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Infrastructure.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace PamAutomation.Tests.Infrastructure.Configuration;

[TestClass]
public sealed class DpapiFavoriteCollectionStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_ProtectedState_RoundTripsCollectionSelection()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"PamAutomation-collections-{Guid.NewGuid():N}");
        try
        {
            var store = new DpapiFavoriteCollectionStore(directory);
            var collection = new FavoriteCollection(
                Guid.NewGuid(),
                "Accesos Peter",
                "Soporte",
                [new FavoriteAccountKey("system", "NOSQLBOOSTER", "account")]);
            var state = new FavoriteCollectionsState([collection], collection.Id);

            await store.SaveAsync(state);
            FavoriteCollectionsState loaded = await store.LoadAsync();

            Assert.AreEqual(collection.Id, loaded.ActiveCollectionId);
            Assert.AreEqual("Accesos Peter", loaded.ActiveCollection?.Name);
            Assert.AreEqual("Soporte", loaded.ActiveCollection?.RequestReason);
            Assert.HasCount(1, loaded.ActiveCollection!.Accounts);
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(
                directory,
                DpapiFavoriteCollectionStore.SettingsFileName));
            Assert.IsFalse(System.Text.Encoding.UTF8.GetString(bytes).Contains("Accesos Peter", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsEmptyState()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"PamAutomation-collections-{Guid.NewGuid():N}");
        var store = new DpapiFavoriteCollectionStore(directory);

        FavoriteCollectionsState state = await store.LoadAsync();

        Assert.IsEmpty(state.Collections);
        Assert.IsNull(state.ActiveCollectionId);
    }

    [TestMethod]
    public async Task LoadAsync_SchemaOne_MigratesRequestReasonAndSelection()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"PamAutomation-collections-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Guid id = Guid.NewGuid();
            string json = $$"""
                {"schemaVersion":1,"activeCollectionId":"{{id}}","useCollectionsForAutomation":true,"collections":[{"id":"{{id}}","name":"Legacy","request":{"startDate":"2026-09-14","startTime":"10:05:00","sessionHours":2,"requestReason":"Soporte anterior"},"accounts":[{"system":"system","application":"app","account":"account"}]}]}
                """;
            byte[] clear = Encoding.UTF8.GetBytes(json);
            byte[] entropy = Encoding.UTF8.GetBytes("PamAutomation/favorite-collections/v1");
            byte[] encrypted = ProtectedData.Protect(clear, entropy, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(
                Path.Combine(directory, DpapiFavoriteCollectionStore.SettingsFileName),
                encrypted);

            FavoriteCollectionsState state = await new DpapiFavoriteCollectionStore(directory).LoadAsync();

            Assert.AreEqual(id, state.ActiveCollectionId);
            Assert.AreEqual("Soporte anterior", state.ActiveCollection?.RequestReason);
            Assert.HasCount(1, state.ActiveCollection!.Accounts);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
