using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;
using PamAutomation.Infrastructure.Configuration;

namespace PamAutomation.Tests.Infrastructure.Configuration;

[TestClass]
public sealed class CollectionMailMigrationTests
{
    [TestMethod]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(2, false)]
    public async Task Load_Legacy_MigratesOnceAndKeepsIndependentCopies(int schema, bool hasGlobalMail)
    {
        string directory = Path.Combine(Path.GetTempPath(), "PamAutomation-mail-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var legacyStore = new DpapiMailSettingsStore(directory);
            var legacy = new MailSettings { SmtpAddress = "principal@example.com", ToRecipients = ["to@example.com"],
                CcRecipients = ["cc@example.com"], Subject = "Legacy subject",
                HtmlTemplate = "<p>Legacy {{REQUEST_IDS}}</p><img src=\"cid:logo\" />",
                InlineImages = [new OutlookInlineImage("logo", "logo.png", "image/png", [1, 2, 3])] };
            if (hasGlobalMail) await legacyStore.SaveAsync(legacy);
            Guid allId = Guid.NewGuid();
            Guid customId = Guid.NewGuid();
            string json = $$"""
                {"schemaVersion":{{schema}},"activeCollectionId":"{{customId}}","collections":[
                  {"id":"{{allId}}","name":"All favorites","isAllFavorites":true,"requestReason":"Reason","request":{"requestReason":"Reason"},"accounts":[{"system":"System","application":"App","account":"Account"}]},
                  {"id":"{{customId}}","name":"Custom","requestReason":"Reason","request":{"requestReason":"Reason"},"accounts":[{"system":"System","application":"App","account":"Account"}]}]}
                """;
            string path = Path.Combine(directory, DpapiFavoriteCollectionStore.SettingsFileName);
            await File.WriteAllBytesAsync(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(json),
                Encoding.UTF8.GetBytes("PamAutomation/favorite-collections/v1"), DataProtectionScope.CurrentUser));
            var store = new DpapiFavoriteCollectionStore(directory);
            FavoriteCollectionsState migrated = await store.LoadAsync();
            Assert.AreEqual(3, migrated.SchemaVersion);
            Assert.AreEqual(customId, migrated.ActiveCollectionId);
            foreach (FavoriteCollection collection in migrated.Collections)
            {
                if (!hasGlobalMail) { Assert.IsNull(collection.Mail); continue; }
                Assert.AreEqual(legacy.Subject, collection.Mail!.Subject);
                Assert.AreEqual(legacy.HtmlTemplate, collection.Mail.HtmlTemplate);
                CollectionAssert.AreEqual(legacy.ToRecipients.ToArray(), collection.Mail.ToRecipients.ToArray());
                CollectionAssert.AreEqual(legacy.CcRecipients.ToArray(), collection.Mail.CcRecipients.ToArray());
                CollectionAssert.AreEqual(legacy.InlineImages[0].Data, collection.Mail.InlineImages[0].Data);
            }
            var service = new FavoriteCollectionService(store);
            FavoriteCollection custom = migrated.ActiveCollection!;
            await service.SaveCollectionAsync(new FavoriteCollection(custom.Id, custom.Name, custom.RequestReason,
                custom.Accounts, mail: new CollectionMailSettings { Subject = "Independent edit" }));
            await legacyStore.SaveAsync(legacy with { Subject = "Changed global must be ignored" });
            var newer = new FavoriteCollection(Guid.NewGuid(), "New", "Reason", custom.Accounts);
            await service.SaveCollectionAsync(newer);
            FavoriteCollectionsState reloaded = await store.LoadAsync();
            Assert.AreEqual("Independent edit", reloaded.ActiveCollection!.Mail!.Subject);
            Assert.AreEqual(hasGlobalMail ? "Legacy subject" : null, reloaded.Collections.Single(c => c.Id == allId).Mail?.Subject);
            Assert.IsNull(reloaded.Collections.Single(c => c.Id == newer.Id).Mail);
            Assert.IsTrue(legacyStore.Exists());
            string encryptedText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));
            Assert.DoesNotContain("Independent edit", encryptedText);
            Assert.DoesNotContain("to@example.com", encryptedText);
            await service.DeleteCollectionAsync(custom.Id);
            Assert.IsFalse((await store.LoadAsync()).Collections.Any(c => c.Id == custom.Id));
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task Signature_ProtectedRoundTripAndReplacement_PreservesInlineResources()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PamAutomation-signature-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DpapiMailSignatureStore(directory);
            Assert.IsNull(await store.LoadAsync());
            var signature = new MailSignature { HtmlContent = "<p>Private signature</p><img src=\"cid:logo\" />",
                InlineImages = [new OutlookInlineImage("logo", "logo.png", "image/png", [1, 2, 3])] };
            await store.SaveAsync(signature);
            MailSignature? loaded = await store.LoadAsync();
            Assert.AreEqual(signature.HtmlContent, loaded!.HtmlContent);
            CollectionAssert.AreEqual(signature.InlineImages[0].Data, loaded.InlineImages[0].Data);
            Assert.DoesNotContain("Private signature", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(
                Path.Combine(directory, DpapiMailSignatureStore.SettingsFileName))));
            await store.SaveAsync(new MailSignature());
            Assert.AreEqual(string.Empty, (await store.LoadAsync())!.HtmlContent);
            Assert.HasCount(1, Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
