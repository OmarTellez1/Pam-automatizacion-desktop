using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace PamAutomation.App.Views;

/// <summary>Shows completed results and copies only their Request IDs on explicit user action.</summary>
public partial class AutomationResultWindow : Window
{
    private readonly string requestIdsText;
    private readonly Action<string> copyText;

    /// <summary>Creates a result window with a separate plain-text Request ID payload.</summary>
    /// <param name="title">The result title.</param>
    /// <param name="message">The summary and Request IDs to display.</param>
    /// <param name="requestIdsText">Only the IDs, one per line, in their original order.</param>
    public AutomationResultWindow(string title, string message, string requestIdsText)
        : this(title, message, requestIdsText, text => Clipboard.SetText(text, TextDataFormat.UnicodeText))
    {
    }

    internal AutomationResultWindow(string title, string message, string requestIdsText, Action<string> copyText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestIdsText);
        this.requestIdsText = requestIdsText;
        this.copyText = copyText ?? throw new ArgumentNullException(nameof(copyText));
        InitializeComponent();
        Title = title;
        ResultText.Text = message;
    }

    private void CopyRequestIds_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            copyText(requestIdsText);
            CopyStatusText.Text = "Request ID copiados al portapapeles.";
            CopyStatusText.Foreground = (Brush)FindResource("SuccessBrush");
        }
        catch (ExternalException)
        {
            CopyStatusText.Text = "No se pudo copiar. El portapapeles está ocupado; vuelve a intentarlo.";
            CopyStatusText.Foreground = (Brush)FindResource("ErrorBrush");
        }
        CopyStatusText.Visibility = Visibility.Visible;
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => Close();
}
