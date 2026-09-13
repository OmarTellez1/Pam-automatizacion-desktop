using System.Diagnostics;

namespace PamAutomation.Core.Models.Credentials;

/// <summary>
/// Contains the PAM values required exclusively by the authentication workflow.
/// </summary>
[DebuggerDisplay("PAM credentials (redacted)")]
public sealed class PamCredentials
{
    /// <summary>
    /// Initializes a complete PAM credential set.
    /// </summary>
    /// <param name="username">PAM account name.</param>
    /// <param name="password">PAM account password.</param>
    /// <param name="totpSeed">Base32 enrollment seed used to generate TOTP codes.</param>
    public PamCredentials(string username, string password, string totpSeed)
    {
        Username = RequireValue(username, nameof(username));
        Password = RequireValue(password, nameof(password));
        TotpSeed = RequireValue(totpSeed, nameof(totpSeed));
    }

    /// <summary>
    /// Gets the PAM account name.
    /// </summary>
    public string Username { get; }

    /// <summary>
    /// Gets the PAM account password.
    /// </summary>
    public string Password { get; }

    /// <summary>
    /// Gets the Base32 enrollment seed used to generate TOTP codes.
    /// </summary>
    public string TotpSeed { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "PAM credentials (redacted)";
    }

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El valor de la credencial es obligatorio.", parameterName);
        }

        return value;
    }
}
