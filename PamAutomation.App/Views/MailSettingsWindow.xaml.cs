using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.Views;

public partial class MailSettingsWindow : Window
{
    private const string RequestIdsMarker = "{{REQUEST_IDS}}";
    private readonly string smtpAddress;
    private readonly ILogger<MailSettingsWindow> logger;

    public MailSettingsWindow(
        MailSettings? current,
        string smtpAddress,
        ILogger<MailSettingsWindow>? logger = null)
    {
        InitializeComponent();
        this.logger = logger ?? NullLogger<MailSettingsWindow>.Instance;
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

    private void InsertTable_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!FlowDocumentTableInserter.TryInsertTwoByTwo(BodyEditor, RequestIdsMarker))
            {
                UrgentMessageBox.Show(
                    "Ubica el cursor en una línea normal fuera de otra tabla y vuelve a intentarlo.",
                    "No se puede insertar la tabla aquí",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            UpdateMarkerStatus();
            logger.LogInformation("A 2x2 mail template table was inserted at the editor caret.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Manual mail table insertion failed safely.");
            UrgentMessageBox.Show(
                "No fue posible insertar la tabla en esa posición. Ubica el cursor en una línea normal y vuelve a intentarlo.",
                "No se pudo insertar la tabla",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void InsertRequestIds_Click(object sender, RoutedEventArgs e)
    {
        BodyEditor.CaretPosition.InsertTextInRun(RequestIdsMarker);
        BodyEditor.Focus();
        UpdateMarkerStatus();
    }

    private void BodyEditor_TextChanged(object sender, TextChangedEventArgs e) => UpdateMarkerStatus();

    private void BodyEditor_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        try
        {
            if (!RichClipboardHtmlImporter.TryReadHtml(e.DataObject, out string html))
            {
                if (e.DataObject.GetDataPresent(DataFormats.Rtf, autoConvert: true))
                {
                    logger.LogInformation("Rich clipboard paste is using the RTF fallback because HTML is unavailable.");
                    e.FormatToApply = DataFormats.Rtf;
                }
                else
                {
                    logger.LogInformation("Clipboard paste contains no supported rich format; default paste will continue.");
                }
                return;
            }

            BitmapSource? fallbackImage = e.DataObject.GetDataPresent(DataFormats.Bitmap, autoConvert: true)
                ? e.DataObject.GetData(DataFormats.Bitmap, autoConvert: true) as BitmapSource
                : null;
            logger.LogInformation(
                "Rich HTML clipboard paste started. HtmlLength={HtmlLength}, HasBitmapFallback={HasBitmapFallback}.",
                html.Length,
                fallbackImage is not null);
            FlowDocument imported = RichClipboardHtmlImporter.Import(html, fallbackImage);

            using var package = new MemoryStream();
            new TextRange(imported.ContentStart, imported.ContentEnd).Save(package, DataFormats.XamlPackage);
            package.Position = 0;
            e.CancelCommand();
            BodyEditor.Selection.Load(package, DataFormats.XamlPackage);
            BodyEditor.Focus();
            UpdateMarkerStatus();
            logger.LogInformation(
                "Rich HTML clipboard paste completed. BlockCount={BlockCount}, TableCount={TableCount}.",
                imported.Blocks.Count,
                imported.Blocks.OfType<Table>().Count());
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or
                                          OutlookHtmlCompositionException or ArgumentException or
                                          InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            e.CancelCommand();
            logger.LogError(exception, "Rich clipboard paste failed safely.");
            UrgentMessageBox.Show(
                "No fue posible pegar ese contenido enriquecido. Intenta copiar nuevamente la firma completa.",
                "Firma no compatible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (CountMarkers() != 1)
        {
            UrgentMessageBox.Show($"Incluye {RequestIdsMarker} exactamente una vez.", "Marcador obligatorio",
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
            UrgentMessageBox.Show(exception.Message, "Contenido no compatible",
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
