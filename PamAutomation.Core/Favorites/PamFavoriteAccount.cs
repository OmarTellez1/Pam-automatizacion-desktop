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
    {
        SystemName = RequireValue(systemName, nameof(systemName));
        ApplicationName = RequireValue(applicationName, nameof(applicationName));
        AccountName = RequireValue(accountName, nameof(accountName));
    }

    /// <summary>Gets the PAM system name.</summary>
    public string SystemName { get; }

    /// <summary>Gets the PAM application name.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the PAM account name.</summary>
    public string AccountName { get; }

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
