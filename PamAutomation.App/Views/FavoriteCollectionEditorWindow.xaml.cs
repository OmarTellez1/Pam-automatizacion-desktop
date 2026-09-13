using System.Collections.ObjectModel;
using System.Windows;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Favorites;

namespace PamAutomation.App.Views;

/// <summary>Creates or edits one named collection without contacting PAM.</summary>
public partial class FavoriteCollectionEditorWindow : Window
{
    private readonly FavoriteCollection? existing;

    /// <summary>Initializes the editor from the protected Favorites catalog.</summary>
    public FavoriteCollectionEditorWindow(
        IReadOnlyList<FavoriteCatalogEntry> favorites,
        FavoriteCollection? existing)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        InitializeComponent();
        this.existing = existing;
        HashSet<FavoriteAccountKey> selected = existing?.Accounts.ToHashSet() ?? [];
        Favorites = new ObservableCollection<FavoriteSelectionItemViewModel>(favorites.Select(entry =>
            new FavoriteSelectionItemViewModel(entry, selected.Contains(FavoriteAccountKey.From(entry)))));
        DataContext = this;

        CollectionNameBox.Text = existing?.Name ?? string.Empty;
        ReasonBox.Text = existing?.RequestReason ?? string.Empty;
        if (existing?.IsAllFavorites == true)
        {
            CollectionNameBox.IsReadOnly = true;
            FavoriteSelectionList.IsEnabled = false;
            AllFavoritesNotice.Visibility = Visibility.Visible;
        }
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

        if (string.IsNullOrWhiteSpace(ReasonBox.Text))
        {
            ShowValidation("Ingresa la razón de la petición.");
            return;
        }

        FavoriteAccountKey[] selected = existing?.IsAllFavorites == true
            ? existing.Accounts.ToArray()
            : Favorites
                .Where(static item => item.IsSelected)
                .Select(static item => FavoriteAccountKey.From(item.Entry))
                .ToArray();
        if (selected.Length == 0)
        {
            ShowValidation("Selecciona al menos una solicitud favorita.");
            return;
        }

        try
        {
            Collection = new FavoriteCollection(
                existing?.Id ?? Guid.NewGuid(),
                CollectionNameBox.Text,
                ReasonBox.Text,
                selected,
                existing?.IsAllFavorites == true);
            DialogResult = true;
        }
        catch (ArgumentException exception)
        {
            ShowValidation(exception.Message);
        }
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationBorder.Visibility = Visibility.Visible;
    }
}
