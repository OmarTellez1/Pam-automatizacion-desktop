using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Infrastructure.Configuration;

namespace PamAutomation.Tests.Infrastructure.Configuration;

[TestClass]
public sealed class DpapiFavoriteCatalogStoreTests
{
    private string? directory;

    [TestCleanup]
    public void Cleanup()
    {
        if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_ValidSnapshot_RoundTripsProtectedDocument()
    {
        directory = Path.Combine(Path.GetTempPath(), "PamAutomation.Tests", Guid.NewGuid().ToString("N"));
        var store = new DpapiFavoriteCatalogStore(directory);
        var snapshot = new FavoriteCatalogSnapshot(
            new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero),
            [new FavoriteCatalogEntry("system", "NOSQLBOOSTER", "account", "10.31.32.25")]);

        await store.SaveAsync(snapshot);
        FavoriteCatalogSnapshot? loaded = await store.LoadAsync();

        Assert.IsTrue(store.Exists());
        Assert.IsNotNull(loaded);
        Assert.AreEqual(snapshot.RefreshedAtUtc, loaded.RefreshedAtUtc);
        Assert.AreEqual(snapshot.Entries[0], loaded.Entries[0]);
        byte[] protectedBytes = await File.ReadAllBytesAsync(
            Path.Combine(directory, DpapiFavoriteCatalogStore.SettingsFileName));
        string protectedText = Encoding.UTF8.GetString(protectedBytes);
        Assert.DoesNotContain("NOSQLBOOSTER", protectedText);
        Assert.DoesNotContain("10.31.32.25", protectedText);
    }

    [TestMethod]
    public async Task LoadAsync_MissingDocument_ReturnsNullWithoutCreatingDirectory()
    {
        directory = Path.Combine(Path.GetTempPath(), "PamAutomation.Tests", Guid.NewGuid().ToString("N"));
        var store = new DpapiFavoriteCatalogStore(directory);

        FavoriteCatalogSnapshot? loaded = await store.LoadAsync();

        Assert.IsNull(loaded);
        Assert.IsFalse(Directory.Exists(directory));
    }

    [TestMethod]
    public void Constructor_RelativeDirectory_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new DpapiFavoriteCatalogStore("relative"));
    }
}
