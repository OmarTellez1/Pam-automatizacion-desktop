using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Tests.Core.Outlook;

[TestClass]
public sealed class CollectionMailComposerTests
{
    [TestMethod]
    public void Resolve_TwoCollectionsAndChangingSignature_UsesIndependentContentAndPrincipalSender()
    {
        FavoriteCollection first = Collection("First", "first");
        FavoriteCollection second = Collection(FavoriteCollection.AllFavoritesName, "second");
        var signature = new MailSignature { HtmlContent = "<p>Firma común</p>" };
        MailSettings firstMail = CollectionMailComposer.Resolve(first, "principal@example.com", signature);
        MailSettings secondMail = CollectionMailComposer.Resolve(second, "principal@example.com", signature);
        Assert.AreEqual("first@example.com", firstMail.ToRecipients[0]);
        Assert.AreEqual("second@example.com", secondMail.ToRecipients[0]);
        Assert.AreEqual("second.cc@example.com", secondMail.CcRecipients[0]);
        Assert.AreEqual("second subject", secondMail.Subject);
        Assert.AreEqual(firstMail.SmtpAddress, secondMail.SmtpAddress);
        Assert.AreEqual("principal@example.com", secondMail.SmtpAddress);
        string body = OutlookHtmlComposer.Compose(secondMail.HtmlTemplate, ["123", "456"]);
        Assert.Contains("second content", body);
        Assert.DoesNotContain("first content", body);
        Assert.AreEqual(1, body.Split("Firma común").Length - 1);
        Assert.IsTrue(body.IndexOf("456", StringComparison.Ordinal) < body.IndexOf("Firma común", StringComparison.Ordinal));
        MailSettings changed = CollectionMailComposer.Resolve(first, "principal@example.com",
            signature with { HtmlContent = "<p>Firma nueva</p>" });
        Assert.DoesNotContain("Firma común", changed.HtmlTemplate);
        Assert.Contains("Firma nueva", changed.HtmlTemplate);
        Assert.DoesNotContain("Firma", first.Mail!.HtmlTemplate);
        Assert.DoesNotContain("Firma", second.Mail!.HtmlTemplate);
    }

    [TestMethod]
    public void Resolve_InlineIdCollision_PreservesBothImagesAndSanitizesSignature()
    {
        var image = new OutlookInlineImage("image1", "image.png", "image/png", [1, 2, 3]);
        FavoriteCollection collection = Collection("First", "first", "<p>{{REQUEST_IDS}}</p><img src=\"cid:image1\" />", [image]);
        var signature = new MailSignature { HtmlContent = "<p onclick=\"bad()\">Firma</p><script>bad()</script><img src=\"cid:image1\" />", InlineImages = [image] };
        MailSettings mail = CollectionMailComposer.Resolve(collection, "principal@example.com", signature);
        Assert.HasCount(2, mail.InlineImages);
        Assert.AreNotEqual(mail.InlineImages[0].ContentId, mail.InlineImages[1].ContentId);
        Assert.DoesNotContain("bad()", mail.HtmlTemplate);
        Assert.Contains("cid:signature_image1", mail.HtmlTemplate);
        Assert.Contains("cid:image1", collection.Mail!.HtmlTemplate);
    }

    [TestMethod]
    public void Resolve_MissingMail_NamesCollectionWithoutFallback()
    {
        var collection = new FavoriteCollection(Guid.NewGuid(), "Sin correo", "Reason", []);
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CollectionMailComposer.Resolve(collection, "principal@example.com", null));
        Assert.Contains("Sin correo", exception.Message);
    }

    [TestMethod]
    public void Signature_EmptyAllowed_RequestIdsForbidden()
    {
        Assert.AreEqual(string.Empty, CollectionMailComposer.ValidateSignature(new MailSignature()).HtmlContent);
        Assert.ThrowsExactly<InvalidOperationException>(() => CollectionMailComposer.ValidateSignature(
            new MailSignature { HtmlContent = "<p>{{REQUEST_IDS}}</p>" }));
    }

    internal static FavoriteCollection Collection(string name, string prefix, string? html = null, IReadOnlyList<OutlookInlineImage>? images = null) =>
        new(Guid.NewGuid(), name, "Reason", [new FavoriteAccountKey("System", "App", "Account")],
            name == FavoriteCollection.AllFavoritesName, new CollectionMailSettings
            {
                ToRecipients = [prefix + "@example.com"], CcRecipients = [prefix + ".cc@example.com"],
                Subject = prefix + " subject", HtmlTemplate = html ?? "<p>" + prefix + " content {{REQUEST_IDS}}</p>",
                InlineImages = images ?? []
            });
}
