using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.Views;

public partial class MailSettingsWindow : Window
{
    public MailSettingsWindow(MailSettings? current)
    {
        InitializeComponent();
        if (current is null)
        {
            BodyEditor.Document = FlowDocumentHtmlConverter.FromXhtml("<p>{{REQUEST_IDS}}</p>");
            return;
        }

        SmtpBox.Text = current.SmtpAddress;
        SubjectBox.Text = current.Subject;
        ToBox.Text = string.Join("; ", current.ToRecipients);
        CcBox.Text = string.Join("; ", current.CcRecipients);
        BodyEditor.Document = FlowDocumentHtmlConverter.FromXhtml(current.HtmlTemplate, current.InlineImages);
    }

    public MailSettings? Settings { get; private set; }
    public bool SendTestRequested { get; private set; }

    private void Bold_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBold.Execute(null, BodyEditor);
    private void Italic_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleItalic.Execute(null, BodyEditor);
    private void Underline_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleUnderline.Execute(null, BodyEditor);
    private void Bullets_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBullets.Execute(null, BodyEditor);
    private void Table_Click(object sender, RoutedEventArgs e)
    {
        var table = new Table();
        table.Columns.Add(new TableColumn());
        table.Columns.Add(new TableColumn());
        var group = new TableRowGroup();
        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run("Texto"))));
            row.Cells.Add(new TableCell(new Paragraph(new Run("Texto"))));
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        BodyEditor.Document.Blocks.Add(table);
    }

    private void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            MessageBox.Show("El portapapeles no contiene una imagen.", "Pegar imagen", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var image = new Image { Source = Clipboard.GetImage(), MaxWidth = 560 };
        BodyEditor.CaretPosition.InsertTextInRun(string.Empty);
        var container = new InlineUIContainer(image, BodyEditor.CaretPosition);
        BodyEditor.CaretPosition = container.ElementEnd;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Save(sendTest: false);
    }

    private void SaveAndTest_Click(object sender, RoutedEventArgs e)
    {
        Save(sendTest: true);
    }

    private void Save(bool sendTest)
    {
        try
        {
            FlowDocumentMailContent content = FlowDocumentHtmlConverter.ToMailContent(BodyEditor.Document);
            Settings = new MailSettings
            {
                SmtpAddress = SmtpBox.Text,
                Subject = SubjectBox.Text,
                ToRecipients = SplitRecipients(ToBox.Text),
                CcRecipients = SplitRecipients(CcBox.Text),
                HtmlTemplate = content.HtmlTemplate,
                InlineImages = content.InlineImages
            };
            SendTestRequested = sendTest;
            DialogResult = true;
        }
        catch (Exception exception) when (exception is NotSupportedException or OutlookHtmlCompositionException)
        {
            MessageBox.Show(exception.Message, "Contenido no compatible", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string[] SplitRecipients(string value) => value
        .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
