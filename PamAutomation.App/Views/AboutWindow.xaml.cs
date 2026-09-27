using System.Windows;

namespace PamAutomation.App.Views;

/// <summary>Displays the public application version and authorship information.</summary>
public partial class AboutWindow : Window
{
    /// <summary>Initializes the application information dialog.</summary>
    public AboutWindow()
    {
        InitializeComponent();
        VersionText = typeof(AboutWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.3";
        DataContext = this;
    }

    /// <summary>Gets the three-component application version displayed to the user.</summary>
    public string VersionText { get; }
}
