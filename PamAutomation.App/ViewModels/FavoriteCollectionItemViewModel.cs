using CommunityToolkit.Mvvm.ComponentModel;
using PamAutomation.Core.Favorites;

namespace PamAutomation.App.ViewModels;

/// <summary>Presents one saved collection and its exclusive automation selection.</summary>
public sealed partial class FavoriteCollectionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isActive;

    /// <summary>Initializes one collection row.</summary>
    public FavoriteCollectionItemViewModel(FavoriteCollection collection, bool isActive, Guid? lockedCollectionId = null)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        this.isActive = isActive;
        this.lockedCollectionId = lockedCollectionId;
    }
    private readonly Guid? lockedCollectionId;

    /// <summary>Gets the underlying immutable collection.</summary>
    public FavoriteCollection Collection { get; }

    /// <summary>Gets the collection name.</summary>
    public string Name => Collection.Name;

    /// <summary>Gets a concise selection and reason summary.</summary>
    public string Summary => $"{Collection.Accounts.Count} solicitudes · Motivo: {Collection.RequestReason}";

    /// <summary>Gets whether this collection can be selected for automation.</summary>
    public bool CanActivate => lockedCollectionId is null && Collection.Accounts.Count > 0;

    /// <summary>Gets whether the user is allowed to delete this collection.</summary>
    public bool CanDelete => !Collection.IsAllFavorites && lockedCollectionId != Collection.Id;
}
