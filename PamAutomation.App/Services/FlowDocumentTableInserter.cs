using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PamAutomation.App.Services;

internal static class FlowDocumentTableInserter
{
    internal static bool TryInsertTwoByTwo(RichTextBox editor, string requestIdsMarker)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestIdsMarker);
        TextPointer insertion = editor.Selection.IsEmpty
            ? editor.CaretPosition
            : editor.Selection.Start;
        Paragraph? paragraph = insertion.Paragraph;
        if (paragraph?.Parent is not FlowDocument || IsInsideTable(paragraph)) return false;

        if (!editor.Selection.IsEmpty)
        {
            editor.Selection.Text = string.Empty;
            insertion = editor.Selection.Start;
            paragraph = insertion.Paragraph;
            if (paragraph?.Parent is not FlowDocument || IsInsideTable(paragraph)) return false;
        }

        if (IsInsideUnsupportedInline(insertion)) insertion = paragraph.ContentEnd;
        TextPointer trailingStart = insertion.InsertParagraphBreak();
        Paragraph? trailingParagraph = trailingStart.Paragraph;
        if (trailingParagraph?.Parent is not FlowDocument document) return false;

        RemoveMarker(document.Blocks, requestIdsMarker);
        Table table = CreateTwoByTwoTable(requestIdsMarker);
        document.Blocks.InsertBefore(trailingParagraph, table);
        editor.CaretPosition = table.RowGroups[0].Rows[0].Cells[0].ContentStart;
        editor.Focus();
        return true;
    }

    private static Table CreateTwoByTwoTable(string requestIdsMarker)
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(30, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(70, GridUnitType.Star) });
        var group = new TableRowGroup();
        group.Rows.Add(CreateRow("Solicitudes de PAM", requestIdsMarker));
        group.Rows.Add(CreateRow("Motivo del acceso", "Escribe aquí el motivo"));
        table.RowGroups.Add(group);
        return table;
    }

    private static TableRow CreateRow(string label, string value)
    {
        var row = new TableRow();
        row.Cells.Add(CreateCell(label, bold: true));
        row.Cells.Add(CreateCell(value, bold: false));
        return row;
    }

    private static TableCell CreateCell(string text, bool bold)
    {
        Inline content = bold ? new Bold(new Run(text)) : new Run(text);
        return new TableCell(new Paragraph(content))
        {
            BorderBrush = Brushes.DarkGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6)
        };
    }

    private static void RemoveMarker(BlockCollection blocks, string marker)
    {
        foreach (Block block in blocks.ToArray())
        {
            switch (block)
            {
                case Paragraph paragraph:
                    RemoveMarker(paragraph.Inlines, marker);
                    break;
                case List list:
                    foreach (ListItem item in list.ListItems) RemoveMarker(item.Blocks, marker);
                    break;
                case Table table:
                    foreach (TableCell cell in table.RowGroups.SelectMany(static group => group.Rows)
                                 .SelectMany(static row => row.Cells))
                    {
                        RemoveMarker(cell.Blocks, marker);
                    }
                    break;
                case Section section:
                    RemoveMarker(section.Blocks, marker);
                    break;
            }
        }
    }

    private static void RemoveMarker(InlineCollection inlines, string marker)
    {
        foreach (Inline inline in inlines.ToArray())
        {
            switch (inline)
            {
                case Run run:
                    run.Text = run.Text.Replace(marker, string.Empty, StringComparison.Ordinal);
                    break;
                case Span span:
                    RemoveMarker(span.Inlines, marker);
                    break;
            }
        }
    }

    private static bool IsInsideTable(FrameworkContentElement element)
    {
        DependencyObject? current = element;
        while (current is FrameworkContentElement content)
        {
            if (content is TableCell) return true;
            current = content.Parent;
        }
        return false;
    }

    private static bool IsInsideUnsupportedInline(TextPointer pointer)
    {
        DependencyObject? current = pointer.Parent;
        while (current is FrameworkContentElement content && content is not Paragraph)
        {
            if (content is Hyperlink or InlineUIContainer) return true;
            current = content.Parent;
        }
        return false;
    }
}
