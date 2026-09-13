namespace PamAutomation.Core.Favorites;

/// <summary>Contains the last complete Favorites catalog observed from PAM.</summary>
public sealed record FavoriteCatalogSnapshot
{
    /// <summary>Current persistence schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Initializes a complete catalog snapshot.</summary>
    /// <param name="refreshedAtUtc">UTC instant at which the live read completed.</param>
    /// <param name="entries">All validated Favorites rows.</param>
    public FavoriteCatalogSnapshot(DateTimeOffset refreshedAtUtc, IReadOnlyList<FavoriteCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("El catálogo de solicitudes favoritas no puede estar vacío.", nameof(entries));
        }

        RefreshedAtUtc = refreshedAtUtc.ToUniversalTime();
        Entries = entries.ToArray();
    }

    /// <summary>Gets the persistence schema version.</summary>
    public int SchemaVersion => CurrentSchemaVersion;

    /// <summary>Gets the UTC instant of the successful live refresh.</summary>
    public DateTimeOffset RefreshedAtUtc { get; }

    /// <summary>Gets every Favorites row in portal order.</summary>
    public IReadOnlyList<FavoriteCatalogEntry> Entries { get; }
}
