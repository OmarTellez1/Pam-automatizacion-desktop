using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Infrastructure.Configuration;

namespace PamAutomation.Tests.Infrastructure.Configuration;

[TestClass]
public sealed class DpapiOutlookAccountSettingsStoreTests
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "PamAutomationTests",
        Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task SaveAndLoad_ProtectsAddressForCurrentWindowsUser()
    {
        var store = new DpapiOutlookAccountSettingsStore(directory);
        var settings = new OutlookAccountSettings { SmtpAddress = "sender@example.test" };

        await store.SaveAsync(settings);
        byte[] persisted = await File.ReadAllBytesAsync(
            Path.Combine(directory, DpapiOutlookAccountSettingsStore.SettingsFileName));
        OutlookAccountSettings? loaded = await store.LoadAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(settings.SmtpAddress, loaded.SmtpAddress);
        Assert.DoesNotContain(settings.SmtpAddress, System.Text.Encoding.UTF8.GetString(persisted));
    }

    [TestMethod]
    public async Task LoadAsync_CorruptedCiphertext_ReturnsControlledFailure()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(
            Path.Combine(directory, DpapiOutlookAccountSettingsStore.SettingsFileName),
            [1, 2, 3]);
        var store = new DpapiOutlookAccountSettingsStore(directory);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [TestMethod]
    public async Task SaveAsync_InvalidAddress_RejectsWithoutCreatingDocument()
    {
        var store = new DpapiOutlookAccountSettingsStore(directory);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            store.SaveAsync(new OutlookAccountSettings { SmtpAddress = "not an address" }));

        Assert.IsFalse(store.Exists());
    }
}
