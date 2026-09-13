using CommunityToolkit.Mvvm.ComponentModel;
using PamAutomation.Core.Favorites;

namespace PamAutomation.App.ViewModels;

/// <summary>Presents one saved collection and its exclusive automation selection.</summary>
public sealed partial class FavoriteCollectionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isActive;

    /// <summary>Initializes one collection row.</summary>
    public FavoriteCollectionItemViewModel(FavoriteCollection collection, bool isActive)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        this.isActive = isActive;
    }

    /// <summary>Gets the underlying immutable collection.</summary>
    public FavoriteCollection Collection { get; }

    /// <summary>Gets the collection name.</summary>
    public string Name => Collection.Name;

    /// <summary>Gets a concise request and selection summary.</summary>
    public string Summary
    {
        get
        {
            TimeOnly startTime = Collection.Request.StartTime;
            int displayHour = startTime.Hour % 12;
            displayHour = displayHour == 0 ? 12 : displayHour;
            string meridiem = startTime.Hour < 12 ? "AM" : "PM";
            return $"{Collection.Accounts.Count} solicitudes · {Collection.Request.StartDate:dd/MM/yyyy} " +
                   $"{displayHour:00}:{startTime.Minute:00} {meridiem} · " +
                   $"{Collection.Request.SessionHours} hora(s)";
        }
    }

    /// <summary>Gets whether this collection can be selected for automation.</summary>
    public bool CanActivate => Collection.Accounts.Count > 0;
}
