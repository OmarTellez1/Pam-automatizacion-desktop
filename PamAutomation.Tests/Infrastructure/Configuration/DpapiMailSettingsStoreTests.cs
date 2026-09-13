using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;
using PamAutomation.Infrastructure.Configuration;

namespace PamAutomation.Tests.Infrastructure.Configuration;

[TestClass]
public sealed class DpapiMailSettingsStoreTests
{
    private string directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "PamAutomationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task SaveAndLoad_UsesEncryptedCurrentUserDocument()
    {
        var store = new DpapiMailSettingsStore(directory);
        var settings = new MailSettings
        {
            SmtpAddress = "sender@example.test",
            ToRecipients = ["first@example.test"],
            Subject = "Private subject",
            HtmlTemplate = "<p>{{REQUEST_IDS}}</p>",
            InlineImages =
            [
                new OutlookInlineImage(
                    "phase16@example.test",
                    "phase16.png",
                    "image/png",
                    [1, 2, 3, 4])
            ]
        };

        await store.SaveAsync(settings);
        byte[] persisted = await File.ReadAllBytesAsync(Path.Combine(directory, DpapiMailSettingsStore.SettingsFileName));
        MailSettings? loaded = await store.LoadAsync();

        Assert.IsTrue(store.Exists());
        Assert.IsNotNull(loaded);
        Assert.AreEqual(settings.Subject, loaded.Subject);
        Assert.HasCount(1, loaded.InlineImages);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, loaded.InlineImages[0].Data);
        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(persisted).Contains(settings.Subject, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Load_CorruptedCiphertext_RejectsWithoutReturningContent()
    {
        var store = new DpapiMailSettingsStore(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, DpapiMailSettingsStore.SettingsFileName), [1, 2, 3]);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync());
    }
}
