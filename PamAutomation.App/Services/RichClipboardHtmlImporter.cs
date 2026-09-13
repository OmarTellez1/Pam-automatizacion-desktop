using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using HtmlAgilityPack;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.Services;

internal static class RichClipboardHtmlImporter
{
    private const int MaximumClipboardLength = 8_000_000;
    private const int MaximumNodes = 20_000;
    private const int MaximumDepth = 64;
    private static readonly HashSet<string> CopiedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "align", "alt", "border", "cellpadding", "cellspacing", "colspan", "dir", "height",
        "href", "rowspan", "src", "style", "title", "valign", "width"
    };

    internal static bool TryReadHtml(IDataObject dataObject, out string html)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        IEnumerable<string> htmlFormats = dataObject.GetFormats(autoConvert: false)
            .Concat(dataObject.GetFormats(autoConvert: true))
            .Where(static format =>
                format.Equals(DataFormats.Html, StringComparison.OrdinalIgnoreCase) ||
                format.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
                format.EndsWith("+html", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string format in htmlFormats)
        {
            object? value = dataObject.GetData(format, autoConvert: false) ??
                dataObject.GetData(format, autoConvert: true);
            if (TryDecodeHtml(value, out html)) return true;
        }

        if (dataObject.GetDataPresent(DataFormats.Html, autoConvert: true) &&
            TryDecodeHtml(dataObject.GetData(DataFormats.Html, autoConvert: true), out html))
        {
            return true;
        }

        html = string.Empty;
        return false;
    }

    internal static FlowDocument Import(string clipboardHtml, BitmapSource? fallbackImage = null)
    {
        if (string.IsNullOrWhiteSpace(clipboardHtml) || clipboardHtml.Length > MaximumClipboardLength)
        {
            throw new InvalidDataException("El contenido pegado está vacío o excede el tamaño permitido.");
        }

        string fragment = ExtractFragment(clipboardHtml);
        var source = new HtmlDocument();
        source.LoadHtml(fragment);

        var inlineImages = new List<OutlookInlineImage>();
        int nodeCount = 0;
        bool fallbackUsed = false;
        var root = new XElement("root");
        foreach (HtmlNode node in source.DocumentNode.ChildNodes)
        {
            foreach (XNode converted in ConvertNode(
                         node,
                         depth: 0,
                         ref nodeCount,
                         inlineImages,
                         fallbackImage,
                         ref fallbackUsed))
            {
                root.Add(converted);
            }
        }

        string imported = string.Concat(root.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        if (string.IsNullOrWhiteSpace(imported))
        {
            throw new InvalidDataException("El portapapeles no contiene texto o imágenes compatibles.");
        }

        string safe = OutlookHtmlComposer.SanitizeFragment(imported);
        return FlowDocumentHtmlConverter.FromXhtml(safe, inlineImages);
    }

    private static bool TryDecodeHtml(object? value, out string html)
    {
        switch (value)
        {
            case string text when !string.IsNullOrWhiteSpace(text):
                html = text;
                return true;
            case byte[] bytes when bytes.Length > 0:
                html = DecodeUtf8(bytes);
                return !string.IsNullOrWhiteSpace(html);
            case Stream stream when stream.CanRead:
                long originalPosition = stream.CanSeek ? stream.Position : 0;
                try
                {
                    if (stream.CanSeek) stream.Position = 0;
                    using var reader = new StreamReader(
                        stream,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: true,
                        leaveOpen: true);
                    html = reader.ReadToEnd();
                    return !string.IsNullOrWhiteSpace(html);
                }
                finally
                {
                    if (stream.CanSeek) stream.Position = originalPosition;
                }
            default:
                html = string.Empty;
                return false;
        }
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0) length = bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length).TrimStart('\uFEFF');
    }

    private static IEnumerable<XNode> ConvertNode(
        HtmlNode node,
        int depth,
        ref int nodeCount,
        List<OutlookInlineImage> inlineImages,
        BitmapSource? fallbackImage,
        ref bool fallbackUsed)
    {
        nodeCount++;
        if (nodeCount > MaximumNodes || depth > MaximumDepth)
        {
            throw new InvalidDataException("El contenido pegado es demasiado complejo.");
        }

        if (node.NodeType == HtmlNodeType.Text)
        {
            string text = HtmlEntity.DeEntitize(node.InnerText);
            return text.Length == 0 ? [] : [new XText(text)];
        }

        if (node.NodeType != HtmlNodeType.Element)
        {
            return [];
        }

        string elementName = NormalizeElementName(node.Name);
        if (elementName is "html" or "body")
        {
            return ConvertChildren(node, depth, ref nodeCount, inlineImages, fallbackImage, ref fallbackUsed);
        }
        if (elementName == "head")
        {
            return [];
        }

        var element = new XElement(elementName);
        foreach (HtmlAttribute attribute in node.Attributes)
        {
            string attributeName = attribute.Name.ToLowerInvariant();
            if (CopiedAttributes.Contains(attributeName))
            {
                element.SetAttributeValue(attributeName, HtmlEntity.DeEntitize(attribute.Value));
            }
        }

        if (elementName == "img")
        {
            string? source = element.Attribute("src")?.Value;
            OutlookInlineImage? image = TryCreateInlineImage(source);
            if (image is null && !fallbackUsed && fallbackImage is not null)
            {
                image = CreateInlineImage(fallbackImage);
                fallbackUsed = true;
            }
            if (image is not null)
            {
                inlineImages.Add(image);
                element.SetAttributeValue("src", "cid:" + image.ContentId);
            }
        }

        foreach (XNode child in ConvertChildren(
                     node,
                     depth,
                     ref nodeCount,
                     inlineImages,
                     fallbackImage,
                     ref fallbackUsed))
        {
            element.Add(child);
        }
        return [element];
    }

    private static List<XNode> ConvertChildren(
        HtmlNode parent,
        int depth,
        ref int nodeCount,
        List<OutlookInlineImage> inlineImages,
        BitmapSource? fallbackImage,
        ref bool fallbackUsed)
    {
        var converted = new List<XNode>();
        foreach (HtmlNode child in parent.ChildNodes)
        {
            converted.AddRange(ConvertNode(
                child,
                depth + 1,
                ref nodeCount,
                inlineImages,
                fallbackImage,
                ref fallbackUsed));
        }
        return converted;
    }

    private static string ExtractFragment(string clipboardHtml)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";
        int start = clipboardHtml.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        int end = clipboardHtml.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);
        if (start >= 0 && end > start)
        {
            start += startMarker.Length;
            return clipboardHtml[start..end];
        }

        int htmlStart = clipboardHtml.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
        return htmlStart >= 0 ? clipboardHtml[htmlStart..] : clipboardHtml;
    }

    private static string NormalizeElementName(string name)
    {
        int separator = name.LastIndexOf(':');
        string normalized = (separator >= 0 ? name[(separator + 1)..] : name).ToLowerInvariant();
        return normalized.Length > 0 && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? normalized
            : "span";
    }

    private static OutlookInlineImage? TryCreateInlineImage(string? source)
    {
        if (source is null || !source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int separator = source.IndexOf(',');
        if (separator <= 0 || !source[..separator].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            byte[] data = Convert.FromBase64String(source[(separator + 1)..]);
            if (data.Length is 0 or > 5_000_000) return null;
            using var stream = new MemoryStream(data, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return CreateInlineImage(bitmap);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private static OutlookInlineImage CreateInlineImage(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        if (stream.Length > 5_000_000)
        {
            throw new InvalidDataException("Una imagen pegada excede el tamaño permitido.");
        }

        string identifier = Guid.NewGuid().ToString("N");
        return new OutlookInlineImage(
            $"pam-{identifier}@local",
            $"{identifier}.png",
            "image/png",
            stream.ToArray());
    }
}
