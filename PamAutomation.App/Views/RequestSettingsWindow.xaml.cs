using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Views;

public partial class RequestSettingsWindow : Window
{
    public RequestSettingsWindow(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday)
    {
        InitializeComponent();
        DateOnly date = current.StartDate == default ? ecuadorToday : current.StartDate;
        TimeOnly time = current.StartTime == default ? new TimeOnly(9, 0) : current.StartTime;
        StartDatePicker.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        int hour12 = time.Hour % 12;
        HourBox.Text = (hour12 == 0 ? 12 : hour12).ToString(CultureInfo.InvariantCulture);
        MinuteBox.Text = time.Minute.ToString("00", CultureInfo.InvariantCulture);
        MeridiemBox.SelectedIndex = time.Hour < 12 ? 0 : 1;
        DurationBox.Text = current.SessionHours.ToString(CultureInfo.InvariantCulture);
        ReasonBox.Text = current.RequestReason;
        TestModeBox.IsChecked = testModeEnabled;
    }

    public RequestConfigurationEdit? Edit { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker.SelectedDate is not DateTime date ||
            !int.TryParse(HourBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int hour) || hour is < 1 or > 12 ||
            !int.TryParse(MinuteBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int minute) || minute is < 0 or > 59 ||
            !int.TryParse(DurationBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int duration) || duration is < 1 or > 8 ||
            MeridiemBox.SelectedItem is not ComboBoxItem meridiem || string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            MessageBox.Show("Revisa fecha, hora, duración de 1 a 8 horas y razón.", "Solicitud inválida",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int hour24 = hour % 12 + (string.Equals(meridiem.Content?.ToString(), "PM", StringComparison.Ordinal) ? 12 : 0);
        Edit = new RequestConfigurationEdit(
            new RequestSettings
            {
                StartDate = DateOnly.FromDateTime(date),
                StartTime = new TimeOnly(hour24, minute),
                SessionHours = duration,
                RequestReason = ReasonBox.Text.Trim()
            },
            TestModeBox.IsChecked == true);
        DialogResult = true;
    }
}
