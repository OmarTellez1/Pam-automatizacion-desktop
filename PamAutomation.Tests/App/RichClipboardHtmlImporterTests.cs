using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class RichClipboardHtmlImporterTests
{
    [STATestMethod]
    public void TryReadHtml_TextHtmlStream_DecodesClipboardPayload()
    {
        const string expected = "<!--StartFragment--><table><tr><td>Contenido</td></tr></table><!--EndFragment-->";
        var data = new DataObject();
        data.SetData("text/html", new MemoryStream(Encoding.UTF8.GetBytes(expected)));

        bool found = RichClipboardHtmlImporter.TryReadHtml(data, out string actual);

        Assert.IsTrue(found);
        Assert.AreEqual(expected, actual);
    }

    [STATestMethod]
    public void TryReadHtml_WindowsHtmlString_ReadsClipboardPayload()
    {
        const string expected = "<!--StartFragment--><p>Contenido</p><!--EndFragment-->";
        var data = new DataObject(DataFormats.Html, expected);

        bool found = RichClipboardHtmlImporter.TryReadHtml(data, out string actual);

        Assert.IsTrue(found);
        Assert.AreEqual(expected, actual);
    }

    [STATestMethod]
    public void Import_HtmlSignature_PreservesSafeFormattingLinkAndInlineImage()
    {
        string image = CreatePngDataUrl();
        string clipboardHtml = $$"""
            Version:1.0
            <!--StartFragment-->
            <div style="background-color:#222222;color:#ffffff;font-family:Georgia;font-size:18px">
              <strong>OMAR TÉLLEZ</strong><br>
              <a href="mailto:omar@example.test" style="color:#4477cc;text-decoration:underline">omar@example.test</a>
              <img src="{{image}}" width="120" height="40">
              <script>contenido-no-seguro</script>
            </div>
            <!--EndFragment-->
            """;

        FlowDocument document = RichClipboardHtmlImporter.Import(clipboardHtml);
        document.Blocks.Add(new Paragraph(new Run(OutlookHtmlComposer.RequestIdsMarker)));
        FlowDocumentMailContent content = FlowDocumentHtmlConverter.ToMailContent(document);

        Assert.Contains("OMAR TÉLLEZ", content.HtmlTemplate);
        Assert.Contains("font-family:'Georgia'", content.HtmlTemplate);
        Assert.Contains("background-color:#222222", content.HtmlTemplate);
        Assert.Contains("href=\"mailto:omar@example.test\"", content.HtmlTemplate);
        Assert.Contains("src=\"cid:", content.HtmlTemplate);
        Assert.DoesNotContain("script", content.HtmlTemplate);
        Assert.DoesNotContain("contenido-no-seguro", content.HtmlTemplate);
        Assert.HasCount(1, content.InlineImages);
        Assert.IsGreaterThan(0, content.InlineImages[0].Data.Length);
    }

    [STATestMethod]
    public void Import_ExternalImageWithBitmapFallback_EmbedsFallbackAsCid()
    {
        var bitmap = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 0, 0, 255, 255 }, 4, 0);

        FlowDocument document = RichClipboardHtmlImporter.Import(
            "<!--StartFragment--><p>Firma</p><img src=\"file:///signature.png\"><!--EndFragment-->",
            bitmap);
        document.Blocks.Add(new Paragraph(new Run(OutlookHtmlComposer.RequestIdsMarker)));
        FlowDocumentMailContent content = FlowDocumentHtmlConverter.ToMailContent(document);

        Assert.Contains("src=\"cid:", content.HtmlTemplate);
        Assert.HasCount(1, content.InlineImages);
    }

    [STATestMethod]
    public void Import_Table_PreservesColumnsSpansAndEditableContentAfterIt()
    {
        const string clipboardHtml = """
            <!--StartFragment-->
            <table border="1" cellpadding="5" style="border-collapse:collapse;width:100%">
              <colgroup><col style="width:30%"><col style="width:70%"></colgroup>
              <tbody>
                <tr><td rowspan="2"><strong>Solicitudes de PAM</strong></td><td>{{REQUEST_IDS}}</td></tr>
                <tr><td>Motor de crédito masivo</td></tr>
              </tbody>
            </table>
            <!--EndFragment-->
            """;

        FlowDocument document = RichClipboardHtmlImporter.Import(clipboardHtml);

        Table table = Assert.IsInstanceOfType<Table>(document.Blocks.FirstBlock);
        Assert.HasCount(2, table.Columns);
        Assert.AreEqual(GridUnitType.Star, table.Columns[0].Width.GridUnitType);
        Assert.AreEqual(30d, table.Columns[0].Width.Value);
        Assert.AreEqual(2, table.RowGroups[0].Rows[0].Cells[0].RowSpan);
        Assert.IsInstanceOfType<Paragraph>(document.Blocks.LastBlock);

        FlowDocumentMailContent content = FlowDocumentHtmlConverter.ToMailContent(document);
        Assert.Contains("<colgroup>", content.HtmlTemplate);
        Assert.Contains("width:30%", content.HtmlTemplate);
        Assert.Contains("rowspan=\"2\"", content.HtmlTemplate);
        Assert.Contains("Motor de crédito masivo", content.HtmlTemplate);
    }

    [STATestMethod]
    public void XamlPackage_LoadAtCaret_InsertsImportedTable()
    {
        FlowDocument imported = RichClipboardHtmlImporter.Import("""
            <!--StartFragment-->
            <p>Antes</p>
            <table border="1"><tr><td>Celda 1</td><td>{{REQUEST_IDS}}</td></tr></table>
            <p>Después</p>
            <!--EndFragment-->
            """);
        var editor = new RichTextBox
        {
            Document = new FlowDocument(new Paragraph(new Run("Contenido existente")))
        };
        editor.CaretPosition = editor.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);

        using var package = new MemoryStream();
        new TextRange(imported.ContentStart, imported.ContentEnd).Save(package, DataFormats.XamlPackage);
        package.Position = 0;
        editor.Selection.Load(package, DataFormats.XamlPackage);

        Assert.IsNotNull(editor.Document.Blocks.OfType<Table>().FirstOrDefault());
        Assert.Contains("Después", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
    }

    private static string CreatePngDataUrl()
    {
        var bitmap = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 255, 255, 255, 255 }, 4, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
}
