namespace PamAutomation.Core.Interfaces.Credentials;

/// <summary>
/// Represents a safe, controlled failure from protected PAM credential storage.
/// </summary>
public sealed class CredentialStoreException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialStoreException"/> class.
    /// </summary>
    /// <param name="errorCode">Stable category for the storage failure.</param>
    /// <param name="message">Description that does not contain credential values.</param>
    /// <param name="innerException">Original technical exception, when available.</param>
    public CredentialStoreException(
        CredentialStoreErrorCode errorCode,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the stable category for the storage failure.
    /// </summary>
    public CredentialStoreErrorCode ErrorCode { get; }
}
