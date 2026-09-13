namespace PamAutomation.Core.Favorites;

/// <summary>Identifies one Favorites row without relying on its grid position.</summary>
public sealed record FavoriteAccountKey
{
    /// <summary>Initializes an exact Favorites identity.</summary>
    public FavoriteAccountKey(string systemName, string applicationName, string accountName)
    {
        SystemName = RequireValue(systemName, nameof(systemName));
        ApplicationName = RequireValue(applicationName, nameof(applicationName));
        AccountName = RequireValue(accountName, nameof(accountName));
    }

    /// <summary>Gets the exact System value.</summary>
    public string SystemName { get; }

    /// <summary>Gets the exact Application value.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the exact Account value.</summary>
    public string AccountName { get; }

    /// <summary>Creates a key from a live favorite account.</summary>
    public static FavoriteAccountKey From(PamFavoriteAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new(account.SystemName, account.ApplicationName, account.AccountName);
    }

    /// <summary>Creates a key from a protected catalog entry.</summary>
    public static FavoriteAccountKey From(FavoriteCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(entry.SystemName, entry.ApplicationName, entry.AccountName);
    }

    /// <summary>Returns whether this key identifies the supplied live account.</summary>
    public bool Matches(PamFavoriteAccount account) =>
        account is not null &&
        string.Equals(SystemName, account.SystemName, StringComparison.Ordinal) &&
        string.Equals(ApplicationName, account.ApplicationName, StringComparison.Ordinal) &&
        string.Equals(AccountName, account.AccountName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => "Favorite account key (redacted)";

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La identidad favorita está incompleta.", parameterName);
        }

        return value.Trim();
    }
}
