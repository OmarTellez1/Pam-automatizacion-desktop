using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class FlowDocumentHtmlConverterTests
{
    [STATestMethod]
    public void RoundTrip_PreservesFormattingTableMarkerAndInlineImage()
    {
        var document = new FlowDocument();
        document.Blocks.Add(new Paragraph(new Bold(new Run("Access " + OutlookHtmlComposer.RequestIdsMarker))));
        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Italic(new Run("Reason")))));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        document.Blocks.Add(table);
        var bitmap = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, 1, 1), new byte[] { 0, 0, 255, 255 }, 4, 0);
        document.Blocks.Add(new Paragraph(new InlineUIContainer(new Image { Source = bitmap, Width = 1 })));

        FlowDocumentMailContent content = FlowDocumentHtmlConverter.ToMailContent(document);
        FlowDocument restored = FlowDocumentHtmlConverter.FromXhtml(content.HtmlTemplate, content.InlineImages);
        FlowDocumentMailContent second = FlowDocumentHtmlConverter.ToMailContent(restored);

        Assert.Contains("<strong>", content.HtmlTemplate);
        Assert.Contains("<table", content.HtmlTemplate);
        Assert.HasCount(1, content.InlineImages);
        Assert.HasCount(1, second.InlineImages);
        Assert.Contains(OutlookHtmlComposer.RequestIdsMarker, second.HtmlTemplate);
    }
}
