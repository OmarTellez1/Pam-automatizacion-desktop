using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Infrastructure.Configuration;

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
                new RequestSettings
                {
                    StartDate = new DateOnly(2026, 9, 14),
                    StartTime = new TimeOnly(10, 5),
                    SessionHours = 2,
                    RequestReason = "Soporte"
                },
                [new FavoriteAccountKey("system", "NOSQLBOOSTER", "account")]);
            var state = new FavoriteCollectionsState([collection], collection.Id, true);

            await store.SaveAsync(state);
            FavoriteCollectionsState loaded = await store.LoadAsync();

            Assert.IsTrue(loaded.UseCollectionsForAutomation);
            Assert.AreEqual(collection.Id, loaded.ActiveCollectionId);
            Assert.AreEqual("Accesos Peter", loaded.ActiveCollection?.Name);
            Assert.AreEqual("Soporte", loaded.ActiveCollection?.Request.RequestReason);
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
        Assert.IsFalse(state.UseCollectionsForAutomation);
    }
}
