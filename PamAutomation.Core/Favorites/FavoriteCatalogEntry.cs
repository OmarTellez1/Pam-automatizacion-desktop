using System.Diagnostics;

namespace PamAutomation.Core.Favorites;

/// <summary>Represents one Favorites identity and the live IP Address used for presentation.</summary>
[DebuggerDisplay("Favorite catalog entry (redacted)")]
public sealed record FavoriteCatalogEntry
{
    /// <summary>Initializes a catalog entry.</summary>
    /// <param name="systemName">Exact System value from Favorites.</param>
    /// <param name="applicationName">Exact Application value from Favorites.</param>
    /// <param name="accountName">Exact Account value from Favorites.</param>
    /// <param name="ipAddress">Exact IP Address value from Favorites.</param>
    public FavoriteCatalogEntry(
        string systemName,
        string applicationName,
        string accountName,
        string ipAddress)
    {
        SystemName = RequireValue(systemName, nameof(systemName));
        ApplicationName = RequireValue(applicationName, nameof(applicationName));
        AccountName = RequireValue(accountName, nameof(accountName));
        IpAddress = RequireValue(ipAddress, nameof(ipAddress));
    }

    /// <summary>Gets the exact System value used as part of the stable local identity.</summary>
    public string SystemName { get; }

    /// <summary>Gets the exact Application value used as part of the stable local identity.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the exact Account value used as part of the stable local identity.</summary>
    public string AccountName { get; }

    /// <summary>Gets the live IP Address displayed to the user.</summary>
    public string IpAddress { get; }

    /// <summary>Gets the concise presentation requested for phase 20.1.</summary>
    public string DisplayName => $"{ApplicationName} - {IpAddress}";

    /// <inheritdoc />
    public override string ToString() => "Favorite catalog entry (redacted)";

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La solicitud favorita está incompleta.", parameterName);
        }

        return value.Trim();
    }
}
