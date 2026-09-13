using System.Diagnostics;

namespace PamAutomation.Core.Favorites;

/// <summary>Identifies one account listed in PAM Favorites.</summary>
[DebuggerDisplay("PAM favorite account (redacted)")]
public sealed record PamFavoriteAccount
{
    /// <summary>Initializes a complete favorite account identity.</summary>
    /// <param name="systemName">PAM system name.</param>
    /// <param name="applicationName">PAM application name.</param>
    /// <param name="accountName">PAM account name.</param>
    public PamFavoriteAccount(string systemName, string applicationName, string accountName)
        : this(systemName, applicationName, accountName, null)
    {
    }

    /// <summary>Initializes a complete favorite account identity with live presentation metadata.</summary>
    /// <param name="systemName">PAM system name.</param>
    /// <param name="applicationName">PAM application name.</param>
    /// <param name="accountName">PAM account name.</param>
    /// <param name="ipAddress">Live IP Address value when it was read from Favorites.</param>
    public PamFavoriteAccount(
        string systemName,
        string applicationName,
        string accountName,
        string? ipAddress)
    {
        SystemName = RequireValue(systemName, nameof(systemName));
        ApplicationName = RequireValue(applicationName, nameof(applicationName));
        AccountName = RequireValue(accountName, nameof(accountName));
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
    }

    /// <summary>Gets the PAM system name.</summary>
    public string SystemName { get; }

    /// <summary>Gets the PAM application name.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the PAM account name.</summary>
    public string AccountName { get; }

    /// <summary>Gets the live IP Address metadata when requested by the caller.</summary>
    public string? IpAddress { get; }

    /// <summary>Compares the stable Favorites identity; live presentation metadata is deliberately excluded.</summary>
    public bool Equals(PamFavoriteAccount? other) =>
        other is not null &&
        string.Equals(SystemName, other.SystemName, StringComparison.Ordinal) &&
        string.Equals(ApplicationName, other.ApplicationName, StringComparison.Ordinal) &&
        string.Equals(AccountName, other.AccountName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(SystemName, ApplicationName, AccountName);

    /// <inheritdoc />
    public override string ToString() => "PAM favorite account (redacted)";

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La identidad de la cuenta favorita está incompleta.", parameterName);
        }

        return value.Trim();
    }
}
