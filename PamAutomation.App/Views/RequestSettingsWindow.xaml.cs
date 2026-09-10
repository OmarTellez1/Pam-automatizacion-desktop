using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Views;

public partial class RequestSettingsWindow : Window
{
    private readonly DateOnly ecuadorToday;

    public RequestSettingsWindow(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday)
    {
        InitializeComponent();
        this.ecuadorToday = ecuadorToday;
        DateOnly date = current.StartDate == default ? ecuadorToday : current.StartDate;
        TimeOnly time = current.StartTime == default ? new TimeOnly(9, 0) : current.StartTime;
        StartDatePicker.DisplayDateStart = ecuadorToday.ToDateTime(TimeOnly.MinValue);
        StartDatePicker.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        int hour12 = time.Hour % 12;
        TimeBox.Text = $"{(hour12 == 0 ? 12 : hour12):00}:{time.Minute:00}";
        MeridiemBox.SelectedIndex = time.Hour < 12 ? 0 : 1;
        DurationBox.Text = current.SessionHours.ToString(CultureInfo.InvariantCulture);
        ReasonBox.Text = current.RequestReason;
        TestModeBox.IsChecked = testModeEnabled;
        UpdateDateDescription();
    }

    public RequestConfigurationEdit? Edit { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker.SelectedDate is not DateTime date || DateOnly.FromDateTime(date) < ecuadorToday)
        {
            ShowValidation("Selecciona una fecha de hoy o posterior.");
            return;
        }

        if (!TimeOnly.TryParseExact(
                TimeBox.Text.Trim(),
                ["h:mm", "hh:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out TimeOnly time12) || time12.Hour is < 1 or > 12)
        {
            ShowValidation("Ingresa la hora con formato hh:mm y selecciona AM o PM.");
            return;
        }

        if (MeridiemBox.SelectedItem is not ComboBoxItem meridiem)
        {
            ShowValidation("Selecciona AM o PM.");
            return;
        }

        if (!int.TryParse(DurationBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int duration) ||
            duration is < 1 or > 8)
        {
            ShowValidation("La duración debe ser un número entero entre 1 y 8 horas.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            ShowValidation("Ingresa la razón de la petición.");
            return;
        }

        int hour24 = time12.Hour % 12 +
            (string.Equals(meridiem.Content?.ToString(), "PM", StringComparison.Ordinal) ? 12 : 0);
        Edit = new RequestConfigurationEdit(
            new RequestSettings
            {
                StartDate = DateOnly.FromDateTime(date),
                StartTime = new TimeOnly(hour24, time12.Minute),
                SessionHours = duration,
                RequestReason = ReasonBox.Text.Trim()
            },
            TestModeBox.IsChecked == true);
        DialogResult = true;
    }

    private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateDateDescription();

    private void UpdateDateDescription()
    {
        DateDescriptionText.Text = StartDatePicker.SelectedDate is DateTime date
            ? date.ToString("dddd, d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-EC"))
            : string.Empty;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationBorder.Visibility = Visibility.Visible;
    }
}
