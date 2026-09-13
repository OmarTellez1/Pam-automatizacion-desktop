namespace PamAutomation.Core.Totp;

/// <summary>
/// Represents an enrollment seed that is not valid Base32 data.
/// </summary>
public sealed class InvalidBase32SecretException : FormatException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidBase32SecretException"/> class.
    /// </summary>
    public InvalidBase32SecretException()
        : base("La clave de enrolamiento TOTP no tiene un formato Base32 válido.")
    {
    }
}
