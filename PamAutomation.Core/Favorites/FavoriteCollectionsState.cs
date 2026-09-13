namespace PamAutomation.Core.Favorites;

/// <summary>Contains every saved collection and the automation selection.</summary>
public sealed record FavoriteCollectionsState
{
    /// <summary>Current persistence schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Initializes a collections state.</summary>
    public FavoriteCollectionsState(
        IReadOnlyList<FavoriteCollection>? collections = null,
        Guid? activeCollectionId = null,
        bool useCollectionsForAutomation = false)
    {
        FavoriteCollection[] copied = collections?.ToArray() ?? [];
        if (copied.Any(static collection => collection is null) ||
            copied.Select(static collection => collection.Id).Distinct().Count() != copied.Length ||
            copied.Select(static collection => collection.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != copied.Length)
        {
            throw new ArgumentException("Las colecciones deben tener identificadores y nombres únicos.", nameof(collections));
        }

        if (activeCollectionId is not null && copied.All(collection => collection.Id != activeCollectionId))
        {
            throw new ArgumentException("La colección activa no existe.", nameof(activeCollectionId));
        }

        if (useCollectionsForAutomation &&
            (activeCollectionId is null || copied.Single(collection => collection.Id == activeCollectionId).Accounts.Count == 0))
        {
            throw new ArgumentException("El modo de colecciones requiere una colección activa con solicitudes.");
        }

        Collections = Array.AsReadOnly(copied);
        ActiveCollectionId = activeCollectionId;
        UseCollectionsForAutomation = useCollectionsForAutomation;
    }

    /// <summary>Gets the persistence schema version.</summary>
    public int SchemaVersion => CurrentSchemaVersion;

    /// <summary>Gets all saved collections.</summary>
    public IReadOnlyList<FavoriteCollection> Collections { get; }

    /// <summary>Gets the collection selected for automation, when one exists.</summary>
    public Guid? ActiveCollectionId { get; }

    /// <summary>Gets whether automation must use the active collection instead of all Favorites.</summary>
    public bool UseCollectionsForAutomation { get; }

    /// <summary>Gets the active collection, when one exists.</summary>
    public FavoriteCollection? ActiveCollection => ActiveCollectionId is Guid id
        ? Collections.Single(collection => collection.Id == id)
        : null;
}
