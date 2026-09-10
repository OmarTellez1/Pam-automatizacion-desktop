using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.Services;

/// <summary>Converts the supported native WPF document subset to sanitized XHTML and back.</summary>
public static class FlowDocumentHtmlConverter
{
    public static string ToXhtml(FlowDocument document)
        => ToMailContent(document).HtmlTemplate;

    public static FlowDocumentMailContent ToMailContent(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var images = new List<OutlookInlineImage>();
        var root = new XElement("root", document.Blocks.Select(block => ConvertBlock(block, images)));
        string fragment = string.Concat(root.Nodes().Select(static node => node.ToString(SaveOptions.DisableFormatting)));
        return new FlowDocumentMailContent(OutlookHtmlComposer.SanitizeTemplate(fragment), images);
    }

    public static FlowDocument FromXhtml(string? html, IReadOnlyList<OutlookInlineImage>? inlineImages = null)
    {
        var document = new FlowDocument();
        Dictionary<string, OutlookInlineImage> images = (inlineImages ?? [])
            .ToDictionary(static image => image.ContentId, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(html))
        {
            document.Blocks.Add(new Paragraph(new Run(OutlookHtmlComposer.RequestIdsMarker)));
            return document;
        }

        string safe = OutlookHtmlComposer.SanitizeTemplate(html);
        XElement root = XElement.Parse("<root>" + safe + "</root>", LoadOptions.PreserveWhitespace);
        foreach (XNode node in root.Nodes())
        {
            Block? block = ConvertElementToBlock(node, images);
            if (block is not null) document.Blocks.Add(block);
        }
        if (document.Blocks.Count == 0) document.Blocks.Add(new Paragraph());
        return document;
    }

    private static XElement ConvertBlock(Block block, List<OutlookInlineImage> images) => block switch
    {
        Paragraph paragraph => new XElement("p", paragraph.Inlines.Select(inline => ConvertInline(inline, images))),
        List list => new XElement(list.MarkerStyle == TextMarkerStyle.Decimal ? "ol" : "ul",
            list.ListItems.Select(item => new XElement("li", item.Blocks.Select(block => ConvertBlock(block, images))))),
        Table table => ConvertTable(table, images),
        _ => throw new NotSupportedException("El documento contiene un bloque que el correo todavía no admite.")
    };

    private static XElement ConvertTable(Table table, List<OutlookInlineImage> images) => new("table",
        new XAttribute("border", "1"),
        new XAttribute("cellpadding", "4"),
        new XElement("tbody", table.RowGroups.SelectMany(group => group.Rows).Select(row =>
            new XElement("tr", row.Cells.Select(cell => new XElement("td", cell.Blocks.Select(block => ConvertBlock(block, images))))))));

    private static object ConvertInline(Inline inline, List<OutlookInlineImage> images) => inline switch
    {
        Run run => new XText(run.Text),
        Bold bold => new XElement("strong", bold.Inlines.Select(child => ConvertInline(child, images))),
        Italic italic => new XElement("em", italic.Inlines.Select(child => ConvertInline(child, images))),
        Underline underline => new XElement("u", underline.Inlines.Select(child => ConvertInline(child, images))),
        Span span => new XElement("span", span.Inlines.Select(child => ConvertInline(child, images))),
        LineBreak => new XElement("br"),
        InlineUIContainer { Child: Image image } => ConvertImage(image, images),
        _ => throw new NotSupportedException("El documento contiene formato que el correo todavía no admite.")
    };

    private static Block? ConvertElementToBlock(XNode node, IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        if (node is XText text) return string.IsNullOrWhiteSpace(text.Value) ? null : new Paragraph(new Run(text.Value));
        if (node is not XElement element) return null;
        return element.Name.LocalName switch
        {
            "p" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" =>
                CreateParagraph(element.Nodes(), images),
            "ul" or "ol" => ConvertList(element, images),
            "table" => ConvertHtmlTable(element, images),
            _ => CreateParagraph(element.Nodes(), images)
        };
    }

    private static List ConvertList(XElement element, IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        var list = new List { MarkerStyle = element.Name.LocalName == "ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc };
        foreach (XElement itemElement in element.Elements("li"))
        {
            var item = new ListItem();
            foreach (XNode node in itemElement.Nodes())
            {
                Block? block = ConvertElementToBlock(node, images);
                if (block is not null) item.Blocks.Add(block);
            }
            if (item.Blocks.Count == 0) item.Blocks.Add(new Paragraph());
            list.ListItems.Add(item);
        }
        return list;
    }

    private static Table ConvertHtmlTable(XElement element, IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        var table = new Table();
        var group = new TableRowGroup();
        IEnumerable<XElement> rows = element.Descendants("tr");
        foreach (XElement rowElement in rows)
        {
            var row = new TableRow();
            foreach (XElement cellElement in rowElement.Elements().Where(static cell => cell.Name.LocalName is "td" or "th"))
            {
                var cell = new TableCell();
                foreach (XNode node in cellElement.Nodes())
                {
                    Block? block = ConvertElementToBlock(node, images);
                    if (block is not null) cell.Blocks.Add(block);
                }
                if (cell.Blocks.Count == 0) cell.Blocks.Add(new Paragraph());
                row.Cells.Add(cell);
            }
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        return table;
    }

    private static Inline[] ConvertNodesToInlines(IEnumerable<XNode> nodes, IReadOnlyDictionary<string, OutlookInlineImage> images) =>
        nodes.SelectMany(node => ConvertNodeToInlines(node, images)).ToArray();

    private static IEnumerable<Inline> ConvertNodeToInlines(XNode node, IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        if (node is XText text) return [new Run(text.Value)];
        if (node is not XElement element) return [];
        if (element.Name.LocalName == "img")
        {
            string? source = element.Attribute("src")?.Value;
            if (source?.StartsWith("cid:", StringComparison.Ordinal) == true && images.TryGetValue(source[4..], out OutlookInlineImage? image))
                return [new InlineUIContainer(new Image { Source = DecodeImage(image.Data), MaxWidth = 560 })];
            return [];
        }

        Inline[] children = ConvertNodesToInlines(element.Nodes(), images);
        return element.Name.LocalName switch
        {
            "strong" or "b" => [CreateSpan<Bold>(children)],
            "em" or "i" => [CreateSpan<Italic>(children)],
            "u" => [CreateSpan<Underline>(children)],
            "br" => [new LineBreak()],
            _ => [CreateSpan<Span>(children)]
        };
    }

    private static Paragraph CreateParagraph(IEnumerable<XNode> nodes, IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        var paragraph = new Paragraph();
        foreach (Inline inline in ConvertNodesToInlines(nodes, images)) paragraph.Inlines.Add(inline);
        return paragraph;
    }

    private static T CreateSpan<T>(IEnumerable<Inline> children) where T : Span, new()
    {
        var span = new T();
        foreach (Inline inline in children) span.Inlines.Add(inline);
        return span;
    }

    private static XElement ConvertImage(Image image, List<OutlookInlineImage> images)
    {
        if (image.Source is not BitmapSource bitmap) throw new NotSupportedException("Solo se admiten imágenes raster pegadas.");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        string contentId = $"pam-{Guid.NewGuid():N}@local";
        images.Add(new OutlookInlineImage(contentId, $"{Guid.NewGuid():N}.png", "image/png", stream.ToArray()));
        var element = new XElement("img", new XAttribute("src", "cid:" + contentId), new XAttribute("alt", "Imagen inline"));
        if (image.ActualWidth > 0) element.SetAttributeValue("width", Math.Min(9999, (int)Math.Ceiling(image.ActualWidth)));
        return element;
    }

    private static BitmapImage DecodeImage(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

public sealed record FlowDocumentMailContent(string HtmlTemplate, IReadOnlyList<OutlookInlineImage> InlineImages);
