namespace PamAutomation.Core.Favorites;

/// <summary>Defines a named subset of Favorites and the reason used for its requests.</summary>
public sealed record FavoriteCollection
{
    /// <summary>Gets the reserved name of the collection synchronized with every Favorite.</summary>
    public const string AllFavoritesName = "All favorites";

    /// <summary>Initializes a favorite collection.</summary>
    public FavoriteCollection(
        Guid id,
        string name,
        string requestReason,
        IReadOnlyList<FavoriteAccountKey> accounts,
        bool isAllFavorites = false)
    {
        if (id == Guid.Empty) throw new ArgumentException("La colección requiere un identificador.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || name.Any(char.IsControl))
        {
            throw new ArgumentException("El nombre de la colección debe contener entre 1 y 100 caracteres.", nameof(name));
        }

        string normalizedName = name.Trim();
        bool usesReservedName = string.Equals(normalizedName, AllFavoritesName, StringComparison.OrdinalIgnoreCase);
        if (isAllFavorites != usesReservedName)
        {
            throw new ArgumentException($"El nombre «{AllFavoritesName}» está reservado para la colección automática.", nameof(name));
        }
        if (isAllFavorites) normalizedName = AllFavoritesName;

        if (string.IsNullOrWhiteSpace(requestReason) || requestReason.Any(character =>
                char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
        {
            throw new ArgumentException("La razón de la petición es obligatoria y debe contener texto válido.", nameof(requestReason));
        }

        ArgumentNullException.ThrowIfNull(accounts);
        FavoriteAccountKey[] copiedAccounts = accounts.ToArray();
        if (copiedAccounts.Any(static account => account is null) ||
            copiedAccounts.Distinct().Count() != copiedAccounts.Length)
        {
            throw new ArgumentException("La colección contiene identidades favoritas inválidas o repetidas.", nameof(accounts));
        }

        Id = id;
        Name = normalizedName;
        RequestReason = requestReason.Trim();
        Accounts = Array.AsReadOnly(copiedAccounts);
        IsAllFavorites = isAllFavorites;
    }

    /// <summary>Gets the stable local identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the user-visible collection name.</summary>
    public string Name { get; }

    /// <summary>Gets the reason supplied to PAM for every account in this collection.</summary>
    public string RequestReason { get; }

    /// <summary>Gets the exact Favorites identities selected by the user.</summary>
    public IReadOnlyList<FavoriteAccountKey> Accounts { get; }

    /// <summary>Gets whether this collection is synchronized automatically with the entire Favorites catalog.</summary>
    public bool IsAllFavorites { get; }

    /// <inheritdoc />
    public override string ToString() => $"Favorite collection ({Accounts.Count} rows)";
}
