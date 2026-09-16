using System.Windows;
using PamAutomation.Core.Accounts;

namespace PamAutomation.App.Views;

public partial class PamAccountNameWindow : Window
{
    public PamAccountNameWindow(string? currentName = null, bool firstRun = true)
    {
        InitializeComponent();
        NameBox.Text = currentName ?? string.Empty;
        if (!firstRun)
        {
            Title = "Editar nombre";
            HeadingText.Text = "Edita el nombre";
            ExplanationText.Text = "El rol y las credenciales no cambiarán.";
        }
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    public string AccountName { get; private set; } = string.Empty;

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AccountName = PamAccountSummary.NormalizeName(NameBox.Text);
            DialogResult = true;
        }
        catch (ArgumentException exception) { ValidationText.Text = exception.Message; }
    }
}
