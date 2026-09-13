using CommunityToolkit.Mvvm.ComponentModel;
using PamAutomation.Core.Favorites;

namespace PamAutomation.App.ViewModels;

/// <summary>Presents one selectable Favorites row in the collection editor.</summary>
public sealed partial class FavoriteSelectionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    /// <summary>Initializes a selectable catalog entry.</summary>
    public FavoriteSelectionItemViewModel(FavoriteCatalogEntry entry, bool isSelected)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        this.isSelected = isSelected;
    }

    /// <summary>Gets the protected catalog entry.</summary>
    public FavoriteCatalogEntry Entry { get; }

    /// <summary>Gets the concise user-visible label.</summary>
    public string DisplayName => Entry.DisplayName;
}
