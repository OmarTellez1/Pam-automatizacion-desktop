using System.Xml.Linq;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Validation;

namespace PamAutomation.Core.Outlook;

/// <summary>Resolves one collection's content with the principal sender and current signature.</summary>
public static class CollectionMailComposer
{
    /// <summary>Builds an effective template; never writes a signature into collection content.</summary>
    public static MailSettings Resolve(FavoriteCollection collection, string smtpAddress, MailSignature? signature)
    {
        ArgumentNullException.ThrowIfNull(collection);
        CollectionMailSettings content = collection.Mail
            ?? throw new InvalidOperationException($"Configura el correo de la colección «{collection.Name}» en Colecciones → Configurar correo de esta colección.");
        MailSettings mail = ForEditor(content, smtpAddress);
        MailSignature normalizedSignature = ValidateSignature(signature ?? new MailSignature());
        if (!string.IsNullOrWhiteSpace(normalizedSignature.HtmlContent))
        {
            XElement root = XElement.Parse("<root>" + normalizedSignature.HtmlContent + "</root>");
            var images = mail.InlineImages.ToList();
            var usedIds = images.Select(image => image.ContentId).ToHashSet(StringComparer.Ordinal);
            var signatureIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (OutlookInlineImage image in normalizedSignature.InlineImages)
            {
                string id = "signature_" + image.ContentId;
                while (!usedIds.Add(id)) id = "s_" + id;
                signatureIds.Add(image.ContentId, id);
                images.Add(new OutlookInlineImage(id, image.FileName, image.ContentType, image.Data));
            }
            foreach (XElement element in root.Descendants("img"))
            {
                string source = (string?)element.Attribute("src") ?? string.Empty;
                if (source.StartsWith("cid:", StringComparison.Ordinal) && signatureIds.TryGetValue(source[4..], out string? id))
                    element.SetAttributeValue("src", "cid:" + id);
            }
            string signatureHtml = string.Concat(root.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
            mail = mail with { HtmlTemplate = mail.HtmlTemplate + signatureHtml, InlineImages = images.ToArray() };
        }
        return new MailSettingsValidator().ValidateAndNormalize(mail);
    }

    /// <summary>Supplies the principal sender for editing without adding the shared signature.</summary>
    public static MailSettings ForEditor(CollectionMailSettings content, string smtpAddress) => new()
    {
        SmtpAddress = smtpAddress,
        ToRecipients = content.ToRecipients.ToArray(),
        CcRecipients = content.CcRecipients.ToArray(),
        Subject = content.Subject,
        HtmlTemplate = content.HtmlTemplate,
        InlineImages = content.InlineImages.ToArray()
    };

    /// <summary>Sanitizes a signature and validates its inline resources without requiring a marker.</summary>
    public static MailSignature ValidateSignature(MailSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        if (signature.SchemaVersion != MailSignature.CurrentSchemaVersion)
            throw new InvalidDataException("La firma tiene una versión incompatible.");
        string safe = string.IsNullOrWhiteSpace(signature.HtmlContent)
            ? string.Empty : OutlookHtmlComposer.SanitizeFragment(signature.HtmlContent);
        if (safe.Contains(OutlookHtmlComposer.RequestIdsMarker, StringComparison.Ordinal))
            throw new InvalidOperationException("La firma no debe contener el marcador de Request ID; ese marcador pertenece al contenido de la colección.");
        OutlookMessageValidator.ValidateInlineImages(safe, signature.InlineImages);
        return signature with { HtmlContent = safe, InlineImages = signature.InlineImages.ToArray() };
    }
}
