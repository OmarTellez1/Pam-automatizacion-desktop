using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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

        string safe = OutlookHtmlComposer.SanitizeFragment(html);
        XElement root = XElement.Parse("<root>" + safe + "</root>", LoadOptions.PreserveWhitespace);
        foreach (XNode node in root.Nodes())
        {
            Block? block = ConvertElementToBlock(node, images);
            if (block is not null) document.Blocks.Add(block);
        }
        if (document.Blocks.Count == 0) document.Blocks.Add(new Paragraph());
        else if (document.Blocks.LastBlock is Table) document.Blocks.Add(new Paragraph());
        return document;
    }

    private static XElement ConvertBlock(Block block, List<OutlookInlineImage> images) => block switch
    {
        Paragraph paragraph => ApplyTextStyle(
            paragraph,
            new XElement("p", paragraph.Inlines.Select(inline => ConvertInline(inline, images))),
            includeAlignment: true),
        List list => new XElement(list.MarkerStyle == TextMarkerStyle.Decimal ? "ol" : "ul",
            list.ListItems.Select(item => new XElement("li", item.Blocks.Select(block => ConvertBlock(block, images))))),
        Table table => ConvertTable(table, images),
        _ => throw new NotSupportedException("El documento contiene un bloque que el correo todavía no admite.")
    };

    private static XElement ConvertTable(Table table, List<OutlookInlineImage> images)
    {
        var element = new XElement(
            "table",
            new XAttribute("border", "1"),
            new XAttribute("cellpadding", "4"),
            new XAttribute("cellspacing", "0"),
            new XAttribute("style", "border-collapse:collapse;width:100%"));
        if (table.Columns.Count > 0)
        {
            element.Add(new XElement(
                "colgroup",
                table.Columns.Select(column => new XElement(
                    "col",
                    new XAttribute("style", "width:" + ToCssColumnWidth(column.Width))))));
        }
        element.Add(new XElement(
            "tbody",
            table.RowGroups.SelectMany(group => group.Rows).Select(row =>
                new XElement("tr", row.Cells.Select(cell => ConvertTableCell(cell, images))))));
        return element;
    }

    private static XElement ConvertTableCell(TableCell cell, List<OutlookInlineImage> images)
    {
        var element = ApplyTextStyle(
            cell,
            new XElement("td", cell.Blocks.Select(block => ConvertBlock(block, images))));
        if (cell.ColumnSpan > 1) element.SetAttributeValue("colspan", cell.ColumnSpan);
        if (cell.RowSpan > 1) element.SetAttributeValue("rowspan", cell.RowSpan);

        var styles = new List<string>();
        if (cell.Padding != default)
        {
            styles.Add($"padding:{ToCssPixels(cell.Padding.Top)}");
        }
        if (cell.BorderThickness != default)
        {
            styles.Add($"border:{ToCssPixels(cell.BorderThickness.Top)} solid " +
                (cell.BorderBrush is SolidColorBrush border ? ToCssColor(border.Color) : "#b8b8b8"));
        }
        AppendStyle(element, styles);
        return element;
    }

    private static object ConvertInline(Inline inline, List<OutlookInlineImage> images) => inline switch
    {
        Run run => ConvertRun(run),
        Hyperlink hyperlink => ConvertHyperlink(hyperlink, images),
        Bold bold => ApplyTextStyle(bold, new XElement("strong", bold.Inlines.Select(child => ConvertInline(child, images)))),
        Italic italic => ApplyTextStyle(italic, new XElement("em", italic.Inlines.Select(child => ConvertInline(child, images)))),
        Underline underline => ApplyTextStyle(underline, new XElement("u", underline.Inlines.Select(child => ConvertInline(child, images)))),
        Span span => ApplyTextStyle(span, new XElement("span", span.Inlines.Select(child => ConvertInline(child, images)))),
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
                CreateParagraph(element, images),
            "ul" or "ol" => ConvertList(element, images),
            "table" => ConvertHtmlTable(element, images),
            _ => CreateInlineParagraph(element, images)
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
        var table = new Table { CellSpacing = 0 };
        foreach (XElement columnElement in element.Descendants("col"))
        {
            string? width = GetCssProperty(columnElement, "width") ?? columnElement.Attribute("width")?.Value;
            table.Columns.Add(new TableColumn
            {
                Width = TryParseColumnWidth(width, out GridLength parsedWidth) ? parsedWidth : GridLength.Auto
            });
        }

        var group = new TableRowGroup();
        int maximumColumnCount = 0;
        IEnumerable<XElement> rows = element.Descendants("tr");
        foreach (XElement rowElement in rows)
        {
            var row = new TableRow();
            int rowColumnCount = 0;
            foreach (XElement cellElement in rowElement.Elements().Where(static cell => cell.Name.LocalName is "td" or "th"))
            {
                int columnSpan = ParseSpan(cellElement.Attribute("colspan")?.Value);
                int rowSpan = ParseSpan(cellElement.Attribute("rowspan")?.Value);
                var cell = new TableCell
                {
                    ColumnSpan = columnSpan,
                    RowSpan = rowSpan,
                    Padding = new Thickness(ParseCssLength(
                        GetCssProperty(cellElement, "padding") ?? element.Attribute("cellpadding")?.Value,
                        4))
                };
                ApplyHtmlStyle(cellElement, cell);
                ApplyTableCellBorder(element, cellElement, cell);
                foreach (XNode node in cellElement.Nodes())
                {
                    Block? block = ConvertElementToBlock(node, images);
                    if (block is not null) cell.Blocks.Add(block);
                }
                if (cell.Blocks.Count == 0) cell.Blocks.Add(new Paragraph());
                row.Cells.Add(cell);
                rowColumnCount += columnSpan;
            }
            group.Rows.Add(row);
            maximumColumnCount = Math.Max(maximumColumnCount, rowColumnCount);
        }
        while (table.Columns.Count < maximumColumnCount) table.Columns.Add(new TableColumn());
        table.RowGroups.Add(group);
        return table;
    }

    private static void ApplyTableCellBorder(XElement tableElement, XElement cellElement, TableCell cell)
    {
        string? borderDeclaration = GetCssProperty(cellElement, "border") ?? GetCssProperty(tableElement, "border");
        bool hasBorderAttribute = ParseCssLength(tableElement.Attribute("border")?.Value, 0) > 0;
        if (borderDeclaration is null && !hasBorderAttribute) return;

        string? widthValue = GetCssProperty(cellElement, "border-width") ??
            borderDeclaration?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        double width = ParseCssLength(widthValue, 1);
        cell.BorderThickness = new Thickness(width);
        if (TryParseBrush(GetCssProperty(cellElement, "border-color") ?? "#b8b8b8", out Brush? brush))
        {
            cell.BorderBrush = brush;
        }
    }

    private static int ParseSpan(string? value) =>
        int.TryParse(value, out int span) && span is >= 1 and <= 100 ? span : 1;

    private static string? GetCssProperty(XElement element, string requestedProperty)
    {
        string? style = element.Attribute("style")?.Value;
        if (string.IsNullOrWhiteSpace(style)) return null;
        foreach (string declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = declaration.IndexOf(':');
            if (separator <= 0 || separator == declaration.Length - 1) continue;
            if (declaration[..separator].Trim().Equals(requestedProperty, StringComparison.OrdinalIgnoreCase))
            {
                return declaration[(separator + 1)..].Trim();
            }
        }
        return null;
    }

    private static bool TryParseColumnWidth(string? value, out GridLength width)
    {
        string normalized = value?.Trim() ?? string.Empty;
        bool isPercentage = normalized.EndsWith('%');
        if (isPercentage) normalized = normalized[..^1];
        else if (normalized.EndsWith("px", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^2];

        bool parsed = double.TryParse(
            normalized,
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out double amount);
        if (!parsed || !double.IsFinite(amount) || amount <= 0 || amount > 9999)
        {
            width = GridLength.Auto;
            return false;
        }

        width = new GridLength(amount, isPercentage ? GridUnitType.Star : GridUnitType.Pixel);
        return true;
    }

    private static double ParseCssLength(string? value, double fallback)
    {
        string normalized = value?.Trim() ?? string.Empty;
        int space = normalized.IndexOf(' ');
        if (space >= 0) normalized = normalized[..space];
        if (normalized.EndsWith("px", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^2];
        return double.TryParse(
                normalized,
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture,
                out double amount) &&
            double.IsFinite(amount) &&
            amount is >= 0 and <= 100
                ? amount
                : fallback;
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
                return [new InlineUIContainer(CreateImageElement(element, image.Data))];
            return [];
        }

        Inline[] children = ConvertNodesToInlines(element.Nodes(), images);
        return element.Name.LocalName switch
        {
            "strong" or "b" => [CreateStyledSpan<Bold>(element, children)],
            "em" or "i" => [CreateStyledSpan<Italic>(element, children)],
            "u" => [CreateStyledSpan<Underline>(element, children)],
            "a" => [CreateHyperlink(element, children)],
            "br" => [new LineBreak()],
            _ => [CreateStyledSpan<Span>(element, children)]
        };
    }

    private static Paragraph CreateParagraph(XElement element, IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        var paragraph = new Paragraph();
        foreach (Inline inline in ConvertNodesToInlines(element.Nodes(), images)) paragraph.Inlines.Add(inline);
        ApplyHtmlStyle(element, paragraph, includeAlignment: true);
        return paragraph;
    }

    private static Paragraph CreateInlineParagraph(
        XElement element,
        IReadOnlyDictionary<string, OutlookInlineImage> images)
    {
        var paragraph = new Paragraph();
        foreach (Inline inline in ConvertNodeToInlines(element, images)) paragraph.Inlines.Add(inline);
        return paragraph;
    }

    private static T CreateStyledSpan<T>(XElement element, IEnumerable<Inline> children) where T : Span, new()
    {
        var span = new T();
        foreach (Inline inline in children) span.Inlines.Add(inline);
        ApplyHtmlStyle(element, span);
        return span;
    }

    private static Hyperlink CreateHyperlink(XElement element, IEnumerable<Inline> children)
    {
        var hyperlink = new Hyperlink();
        foreach (Inline inline in children) hyperlink.Inlines.Add(inline);
        if (Uri.TryCreate(element.Attribute("href")?.Value, UriKind.Absolute, out Uri? target))
        {
            hyperlink.NavigateUri = target;
        }
        ApplyHtmlStyle(element, hyperlink);
        return hyperlink;
    }

    private static object ConvertRun(Run run)
    {
        var span = ApplyTextStyle(run, new XElement("span", new XText(run.Text)));
        return span.Attribute("style") is null ? new XText(run.Text) : span;
    }

    private static XElement ConvertHyperlink(Hyperlink hyperlink, List<OutlookInlineImage> images)
    {
        var element = new XElement("a", hyperlink.Inlines.Select(child => ConvertInline(child, images)));
        if (hyperlink.NavigateUri is not null)
        {
            element.SetAttributeValue("href", hyperlink.NavigateUri.AbsoluteUri);
        }
        return ApplyTextStyle(hyperlink, element);
    }

    private static XElement ApplyTextStyle(TextElement source, XElement element, bool includeAlignment = false)
    {
        var declarations = new List<string>();
        if (source.ReadLocalValue(TextElement.FontFamilyProperty) is FontFamily fontFamily)
        {
            string safeFamily = fontFamily.Source.Replace("'", string.Empty, StringComparison.Ordinal);
            declarations.Add($"font-family:'{safeFamily}'");
        }
        if (source.ReadLocalValue(TextElement.FontSizeProperty) is double fontSize &&
            double.IsFinite(fontSize) && fontSize > 0)
        {
            declarations.Add($"font-size:{fontSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}px");
        }
        if (source.ReadLocalValue(TextElement.FontWeightProperty) is FontWeight fontWeight &&
            fontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight())
        {
            declarations.Add("font-weight:bold");
        }
        if (source.ReadLocalValue(TextElement.FontStyleProperty) is FontStyle fontStyle && fontStyle == FontStyles.Italic)
        {
            declarations.Add("font-style:italic");
        }
        if (source.ReadLocalValue(TextElement.ForegroundProperty) is SolidColorBrush foreground)
        {
            declarations.Add("color:" + ToCssColor(foreground.Color));
        }
        if (source.ReadLocalValue(TextElement.BackgroundProperty) is SolidColorBrush background)
        {
            declarations.Add("background-color:" + ToCssColor(background.Color));
        }
        if (source.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations &&
            decorations.Any(decoration => decoration.Location == TextDecorationLocation.Underline))
        {
            declarations.Add("text-decoration:underline");
        }
        if (includeAlignment && source is Paragraph paragraph &&
            paragraph.ReadLocalValue(Block.TextAlignmentProperty) is TextAlignment alignment)
        {
            declarations.Add("text-align:" + alignment.ToString().ToLowerInvariant());
        }

        if (declarations.Count > 0)
        {
            element.SetAttributeValue("style", string.Join(';', declarations));
        }
        return element;
    }

    private static void ApplyHtmlStyle(XElement element, TextElement target, bool includeAlignment = false)
    {
        string? style = element.Attribute("style")?.Value;
        if (string.IsNullOrWhiteSpace(style)) return;

        foreach (string declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = declaration.IndexOf(':');
            if (separator <= 0 || separator == declaration.Length - 1) continue;
            string property = declaration[..separator].Trim().ToLowerInvariant();
            string value = declaration[(separator + 1)..].Trim();
            switch (property)
            {
                case "font-family":
                    string family = value.Trim('"', '\'');
                    if (family.Length > 0) target.FontFamily = new FontFamily(family);
                    break;
                case "font-size" when TryParseCssFontSize(value, out double size):
                    target.FontSize = size;
                    break;
                case "font-weight" when string.Equals(value, "bold", StringComparison.OrdinalIgnoreCase):
                    target.FontWeight = FontWeights.Bold;
                    break;
                case "font-style" when string.Equals(value, "italic", StringComparison.OrdinalIgnoreCase):
                    target.FontStyle = FontStyles.Italic;
                    break;
                case "color" when TryParseBrush(value, out Brush? foreground):
                    target.Foreground = foreground;
                    break;
                case "background-color" when TryParseBrush(value, out Brush? background):
                    target.Background = background;
                    break;
                case "text-decoration" when target is Inline inline &&
                    value.Contains("underline", StringComparison.OrdinalIgnoreCase):
                    inline.TextDecorations = TextDecorations.Underline;
                    break;
                case "text-align" when includeAlignment && target is Paragraph paragraph &&
                    Enum.TryParse(value, ignoreCase: true, out TextAlignment alignment):
                    paragraph.TextAlignment = alignment;
                    break;
            }
        }
    }

    private static bool TryParseCssFontSize(string value, out double size)
    {
        bool isPoints = value.EndsWith("pt", StringComparison.OrdinalIgnoreCase);
        string numeric = value.EndsWith("px", StringComparison.OrdinalIgnoreCase) || isPoints
            ? value[..^2]
            : value;
        bool parsed = double.TryParse(
            numeric,
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out size);
        if (parsed && isPoints) size *= 96d / 72d;
        return parsed && double.IsFinite(size) && size is >= 6 and <= 200;
    }

    private static bool TryParseBrush(string value, out Brush? brush)
    {
        try
        {
            brush = new BrushConverter().ConvertFromInvariantString(value) as Brush;
            if (brush?.CanFreeze == true) brush.Freeze();
            return brush is not null;
        }
        catch (NotSupportedException)
        {
            brush = null;
            return false;
        }
        catch (FormatException)
        {
            brush = null;
            return false;
        }
    }

    private static string ToCssColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string ToCssColumnWidth(GridLength width) => width.GridUnitType switch
    {
        GridUnitType.Star => ToCssNumber(width.Value) + "%",
        GridUnitType.Pixel => ToCssPixels(width.Value),
        _ => "auto"
    };

    private static string ToCssPixels(double value) => ToCssNumber(value) + "px";

    private static string ToCssNumber(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static void AppendStyle(XElement element, IEnumerable<string> declarations)
    {
        string additions = string.Join(';', declarations.Where(static declaration => declaration.Length > 0));
        if (additions.Length == 0) return;
        string? current = element.Attribute("style")?.Value;
        element.SetAttributeValue(
            "style",
            string.IsNullOrWhiteSpace(current) ? additions : current.TrimEnd(';') + ";" + additions);
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
        double width = double.IsFinite(image.Width) && image.Width > 0 ? image.Width : image.ActualWidth;
        double height = double.IsFinite(image.Height) && image.Height > 0 ? image.Height : image.ActualHeight;
        if (width > 0) element.SetAttributeValue("width", Math.Min(9999, (int)Math.Ceiling(width)));
        if (height > 0) element.SetAttributeValue("height", Math.Min(9999, (int)Math.Ceiling(height)));
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

    private static Image CreateImageElement(XElement element, byte[] data)
    {
        var image = new Image { Source = DecodeImage(data), MaxWidth = 560 };
        if (TryParseImageDimension(element.Attribute("width")?.Value, out double width)) image.Width = width;
        if (TryParseImageDimension(element.Attribute("height")?.Value, out double height)) image.Height = height;
        return image;
    }

    private static bool TryParseImageDimension(string? value, out double dimension)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.EndsWith("px", StringComparison.OrdinalIgnoreCase)) normalized = normalized[..^2];
        return double.TryParse(
                normalized,
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture,
                out dimension) &&
            double.IsFinite(dimension) &&
            dimension is > 0 and <= 560;
    }
}

public sealed record FlowDocumentMailContent(string HtmlTemplate, IReadOnlyList<OutlookInlineImage> InlineImages);
