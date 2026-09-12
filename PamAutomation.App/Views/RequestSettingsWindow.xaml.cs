using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Views;

public partial class RequestSettingsWindow : Window
{
    private readonly DateOnly ecuadorToday;
    private readonly bool testModeEnabled;

    public RequestSettingsWindow(RequestSettings current, bool testModeEnabled, DateOnly ecuadorToday)
    {
        InitializeComponent();
        this.ecuadorToday = ecuadorToday;
        this.testModeEnabled = testModeEnabled;
        DateOnly date = current.StartDate == default ? ecuadorToday : current.StartDate;
        TimeOnly time = current.StartTime == default ? new TimeOnly(9, 0) : current.StartTime;
        StartDatePicker.DisplayDateStart = ecuadorToday.ToDateTime(TimeOnly.MinValue);
        StartDatePicker.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        int hour12 = time.Hour % 12;
        HourBox.ItemsSource = Enumerable.Range(1, 12)
            .Select(hour => hour.ToString("00", CultureInfo.InvariantCulture))
            .ToArray();
        HourBox.SelectedItem = (hour12 == 0 ? 12 : hour12).ToString("00", CultureInfo.InvariantCulture);
        MinuteBox.Text = time.Minute.ToString("00", CultureInfo.InvariantCulture);
        MeridiemBox.SelectedIndex = time.Hour < 12 ? 0 : 1;
        DurationBox.Text = current.SessionHours.ToString(CultureInfo.InvariantCulture);
        ReasonBox.Text = current.RequestReason;
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

        string? hourText = HourBox.SelectedItem as string;
        string? meridiemText = (MeridiemBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        RequestStartTimeInputError timeError = RequestStartTimeInput.TryParse(
            hourText,
            MinuteBox.Text.Trim(),
            meridiemText,
            out TimeOnly startTime);

        if (timeError == RequestStartTimeInputError.InvalidHour)
        {
            ShowValidation("Selecciona una hora entre 01 y 12.");
            return;
        }

        if (timeError == RequestStartTimeInputError.InvalidMinute)
        {
            ShowValidation("Ingresa minutos válidos entre 0 y 59.");
            return;
        }

        if (timeError == RequestStartTimeInputError.InvalidMeridiem)
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

        Edit = new RequestConfigurationEdit(
            new RequestSettings
            {
                StartDate = DateOnly.FromDateTime(date),
                StartTime = startTime,
                SessionHours = duration,
                RequestReason = ReasonBox.Text.Trim()
            },
            testModeEnabled);
        DialogResult = true;
    }

    private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateDateDescription();

    private void MinuteBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(character => !char.IsDigit(character));

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
