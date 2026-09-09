using System.Diagnostics;

namespace PamAutomation.Core.Models.Credentials;

/// <summary>
/// Describes credential fields entered by the user during a create or edit operation.
/// </summary>
/// <remarks>
/// A null, empty, or whitespace-only field preserves its existing protected value.
/// </remarks>
[DebuggerDisplay("PAM credential update (redacted)")]
public sealed class PamCredentialUpdate
{
    /// <summary>
    /// Gets the new PAM account name, or an empty value to preserve the existing name.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the new PAM password, or an empty value to preserve the existing password.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the new Base32 enrollment seed, or an empty value to preserve the existing seed.
    /// </summary>
    public string? TotpSeed { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "PAM credential update (redacted)";
    }
}
