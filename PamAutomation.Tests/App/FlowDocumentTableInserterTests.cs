using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.Services;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class FlowDocumentTableInserterTests
{
    [STATestMethod]
    public void TryInsertTwoByTwo_CaretInMiddle_InsertsBetweenSplitParagraphs()
    {
        var run = new Run("AntesDespués");
        var editor = new RichTextBox { Document = new FlowDocument(new Paragraph(run)) };
        editor.CaretPosition = run.ContentStart.GetPositionAtOffset(5);

        bool inserted = FlowDocumentTableInserter.TryInsertTwoByTwo(
            editor,
            OutlookHtmlComposer.RequestIdsMarker);

        Assert.IsTrue(inserted);
        Block[] blocks = editor.Document.Blocks.ToArray();
        Assert.HasCount(3, blocks);
        Assert.AreEqual("Antes", GetText(Assert.IsInstanceOfType<Paragraph>(blocks[0])));
        Assert.IsInstanceOfType<Table>(blocks[1]);
        Assert.AreEqual("Después", GetText(Assert.IsInstanceOfType<Paragraph>(blocks[2])));
    }

    [STATestMethod]
    public void TryInsertTwoByTwo_ExistingMarker_MovesSingleMarkerIntoTable()
    {
        var first = new Paragraph(new Run(OutlookHtmlComposer.RequestIdsMarker));
        var second = new Paragraph(new Run("Firma"));
        var editor = new RichTextBox { Document = new FlowDocument() };
        editor.Document.Blocks.Add(first);
        editor.Document.Blocks.Add(second);
        editor.CaretPosition = second.ContentStart;

        bool inserted = FlowDocumentTableInserter.TryInsertTwoByTwo(
            editor,
            OutlookHtmlComposer.RequestIdsMarker);

        Assert.IsTrue(inserted);
        string text = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
        Assert.AreEqual(1, text.Split(OutlookHtmlComposer.RequestIdsMarker, StringSplitOptions.None).Length - 1);
        Table table = Assert.IsInstanceOfType<Table>(editor.Document.Blocks.OfType<Table>().Single());
        Assert.AreEqual(
            OutlookHtmlComposer.RequestIdsMarker,
            GetText(table.RowGroups[0].Rows[0].Cells[1]));
    }

    [STATestMethod]
    public void TryInsertTwoByTwo_CaretInsideExistingTable_DoesNotCreateNestedTable()
    {
        var cell = new TableCell(new Paragraph(new Run("Celda")));
        var row = new TableRow();
        row.Cells.Add(cell);
        var group = new TableRowGroup();
        group.Rows.Add(row);
        var table = new Table();
        table.RowGroups.Add(group);
        var editor = new RichTextBox { Document = new FlowDocument() };
        editor.Document.Blocks.Add(table);
        editor.CaretPosition = cell.ContentStart;

        bool inserted = FlowDocumentTableInserter.TryInsertTwoByTwo(
            editor,
            OutlookHtmlComposer.RequestIdsMarker);

        Assert.IsFalse(inserted);
        Assert.HasCount(1, editor.Document.Blocks.OfType<Table>().ToArray());
    }

    private static string GetText(TextElement element) =>
        new TextRange(element.ContentStart, element.ContentEnd).Text.TrimEnd('\r', '\n');
}
