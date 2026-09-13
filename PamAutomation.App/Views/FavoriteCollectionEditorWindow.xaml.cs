using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PamAutomation.App.Services;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Validation;

namespace PamAutomation.App.Views;

/// <summary>Creates or edits one named collection without contacting PAM.</summary>
public partial class FavoriteCollectionEditorWindow : Window
{
    private readonly FavoriteCollection? existing;
    private readonly DateTimeOffset ecuadorNow;
    private readonly RequestSettingsValidator validator = new();

    /// <summary>Initializes the editor from the protected Favorites catalog.</summary>
    public FavoriteCollectionEditorWindow(
        IReadOnlyList<FavoriteCatalogEntry> favorites,
        FavoriteCollection? existing,
        DateTimeOffset ecuadorNow)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        InitializeComponent();
        this.existing = existing;
        this.ecuadorNow = ecuadorNow;
        HashSet<FavoriteAccountKey> selected = existing?.Accounts.ToHashSet() ?? [];
        Favorites = new ObservableCollection<FavoriteSelectionItemViewModel>(favorites.Select(entry =>
            new FavoriteSelectionItemViewModel(entry, selected.Contains(FavoriteAccountKey.From(entry)))));
        DataContext = this;

        DateTimeOffset suggestedStart = ecuadorNow.AddMinutes(10);
        RequestSettings request = existing?.Request ?? new RequestSettings
        {
            StartDate = DateOnly.FromDateTime(suggestedStart.DateTime),
            StartTime = TimeOnly.FromDateTime(suggestedStart.DateTime),
            SessionHours = 1
        };
        CollectionNameBox.Text = existing?.Name ?? string.Empty;
        StartDatePicker.DisplayDateStart = ecuadorNow.Date;
        StartDatePicker.SelectedDate = request.StartDate.ToDateTime(TimeOnly.MinValue);
        int hour12 = request.StartTime.Hour % 12;
        HourBox.ItemsSource = Enumerable.Range(1, 12)
            .Select(hour => hour.ToString("00", CultureInfo.InvariantCulture));
        HourBox.SelectedItem = (hour12 == 0 ? 12 : hour12).ToString("00", CultureInfo.InvariantCulture);
        MinuteBox.Text = request.StartTime.Minute.ToString("00", CultureInfo.InvariantCulture);
        MeridiemBox.SelectedIndex = request.StartTime.Hour < 12 ? 0 : 1;
        DurationBox.Text = request.SessionHours.ToString(CultureInfo.InvariantCulture);
        ReasonBox.Text = request.RequestReason;
    }

    /// <summary>Gets the selectable Favorites rows.</summary>
    public ObservableCollection<FavoriteSelectionItemViewModel> Favorites { get; }

    /// <summary>Gets the collection produced after successful validation.</summary>
    public FavoriteCollection? Collection { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CollectionNameBox.Text))
        {
            ShowValidation("Ingresa un nombre para la colección.");
            return;
        }

        FavoriteAccountKey[] selected = Favorites
            .Where(static item => item.IsSelected)
            .Select(static item => FavoriteAccountKey.From(item.Entry))
            .ToArray();
        if (selected.Length == 0)
        {
            ShowValidation("Selecciona al menos una solicitud favorita.");
            return;
        }

        if (StartDatePicker.SelectedDate is not DateTime date)
        {
            ShowValidation("Selecciona una fecha de inicio.");
            return;
        }

        string? meridiem = (MeridiemBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        RequestStartTimeInputError timeError = RequestStartTimeInput.TryParse(
            HourBox.SelectedItem as string,
            MinuteBox.Text.Trim(),
            meridiem,
            out TimeOnly startTime);
        if (timeError != RequestStartTimeInputError.None)
        {
            ShowValidation(timeError == RequestStartTimeInputError.InvalidMinute
                ? "Ingresa minutos válidos entre 0 y 59."
                : "Selecciona una hora válida y su valor AM o PM.");
            return;
        }

        if (!int.TryParse(DurationBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int duration))
        {
            ShowValidation("La duración debe ser un número entero entre 1 y 8 horas.");
            return;
        }

        var request = new RequestSettings
        {
            StartDate = DateOnly.FromDateTime(date),
            StartTime = startTime,
            SessionHours = duration,
            RequestReason = ReasonBox.Text.Trim()
        };
        ConfigurationValidationResult validation = validator.Validate(request, ecuadorNow);
        if (!validation.IsValid)
        {
            ShowValidation(validation.Failures[0].Message);
            return;
        }

        Collection = new FavoriteCollection(
            existing?.Id ?? Guid.NewGuid(),
            CollectionNameBox.Text,
            request,
            selected);
        DialogResult = true;
    }

    private void MinuteBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(character => !char.IsDigit(character));

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationBorder.Visibility = Visibility.Visible;
    }
}
