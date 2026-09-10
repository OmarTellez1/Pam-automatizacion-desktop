using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.Views;

public partial class MailSettingsWindow : Window
{
    private const string RequestIdsMarker = "{{REQUEST_IDS}}";
    private readonly string smtpAddress;

    public MailSettingsWindow(MailSettings? current, string smtpAddress)
    {
        InitializeComponent();
        this.smtpAddress = string.IsNullOrWhiteSpace(smtpAddress)
            ? throw new ArgumentException("La cuenta remitente es obligatoria.", nameof(smtpAddress))
            : smtpAddress.Trim();
        SenderAddressRun.Text = this.smtpAddress;
        if (current is null)
        {
            BodyEditor.Document = FlowDocumentHtmlConverter.FromXhtml($"<p>{RequestIdsMarker}</p>");
        }
        else
        {
            SubjectBox.Text = current.Subject;
            ToBox.Text = string.Join("; ", current.ToRecipients);
            CcBox.Text = string.Join("; ", current.CcRecipients);
            BodyEditor.Document = FlowDocumentHtmlConverter.FromXhtml(current.HtmlTemplate, current.InlineImages);
        }
        UpdateMarkerStatus();
    }

    public MailSettings? Settings { get; private set; }

    private void Bold_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBold.Execute(null, BodyEditor);
    private void Italic_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleItalic.Execute(null, BodyEditor);
    private void Underline_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleUnderline.Execute(null, BodyEditor);
    private void Bullets_Click(object sender, RoutedEventArgs e) => EditingCommands.ToggleBullets.Execute(null, BodyEditor);

    private void Table_Click(object sender, RoutedEventArgs e)
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn());
        table.Columns.Add(new TableColumn());
        var group = new TableRowGroup();
        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            var row = new TableRow();
            row.Cells.Add(CreateTableCell());
            row.Cells.Add(CreateTableCell());
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        BodyEditor.Document.Blocks.Add(table);
    }

    private static TableCell CreateTableCell() => new(new Paragraph(new Run("Texto")))
    {
        BorderBrush = Brushes.LightGray,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(6)
    };

    private void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            MessageBox.Show("El portapapeles no contiene una imagen.", "Pegar imagen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var image = new Image { Source = Clipboard.GetImage(), MaxWidth = 560 };
        var container = new InlineUIContainer(image, BodyEditor.CaretPosition);
        BodyEditor.CaretPosition = container.ElementEnd;
    }

    private void InsertRequestIds_Click(object sender, RoutedEventArgs e)
    {
        BodyEditor.CaretPosition.InsertTextInRun(RequestIdsMarker);
        BodyEditor.Focus();
        UpdateMarkerStatus();
    }

    private void BodyEditor_TextChanged(object sender, TextChangedEventArgs e) => UpdateMarkerStatus();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (CountMarkers() != 1)
        {
            MessageBox.Show($"Incluye {RequestIdsMarker} exactamente una vez.", "Marcador obligatorio",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            FlowDocumentMailContent content = FlowDocumentHtmlConverter.ToMailContent(BodyEditor.Document);
            Settings = new MailSettings
            {
                SmtpAddress = smtpAddress,
                Subject = SubjectBox.Text,
                ToRecipients = SplitRecipients(ToBox.Text),
                CcRecipients = SplitRecipients(CcBox.Text),
                HtmlTemplate = content.HtmlTemplate,
                InlineImages = content.InlineImages
            };
            DialogResult = true;
        }
        catch (Exception exception) when (exception is NotSupportedException or OutlookHtmlCompositionException)
        {
            MessageBox.Show(exception.Message, "Contenido no compatible",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateMarkerStatus()
    {
        if (!IsInitialized || MarkerStatusBorder is null) return;
        int count = CountMarkers();
        bool valid = count == 1;
        MarkerStatusBorder.Background = new SolidColorBrush(valid
            ? Color.FromRgb(232, 246, 240)
            : Color.FromRgb(255, 235, 238));
        MarkerStatusText.Foreground = new SolidColorBrush(valid
            ? Color.FromRgb(24, 135, 91)
            : Color.FromRgb(195, 59, 74));
        MarkerStatusText.Text = valid
            ? $"El marcador {RequestIdsMarker} está listo para reemplazarse por los ID obtenidos de Favorites."
            : $"Incluye {RequestIdsMarker} exactamente una vez. Apariciones actuales: {count}.";
    }

    private int CountMarkers()
    {
        string text = new TextRange(BodyEditor.Document.ContentStart, BodyEditor.Document.ContentEnd).Text;
        return text.Split(RequestIdsMarker, StringSplitOptions.None).Length - 1;
    }

    private static string[] SplitRecipients(string value) => value
        .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
