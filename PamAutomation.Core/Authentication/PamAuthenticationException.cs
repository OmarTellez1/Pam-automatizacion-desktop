namespace PamAutomation.Core.Authentication;

/// <summary>Represents a stable, non-sensitive PAM authentication failure.</summary>
public sealed class PamAuthenticationException : Exception
{
    /// <summary>Initializes a controlled authentication failure.</summary>
    /// <param name="errorCode">Stable category for the failure.</param>
    /// <param name="innerException">Technical failure retained without including secret values in the message.</param>
    public PamAuthenticationException(PamAuthenticationErrorCode errorCode, Exception? innerException = null)
        : base(GetMessage(errorCode), innerException) => ErrorCode = errorCode;

    /// <summary>Gets the stable category for the failure.</summary>
    public PamAuthenticationErrorCode ErrorCode { get; }

    private static string GetMessage(PamAuthenticationErrorCode errorCode) => errorCode switch
    {
        PamAuthenticationErrorCode.CredentialsRejected => "El portal PAM rechazó las credenciales.",
        PamAuthenticationErrorCode.TotpRejected => "El portal PAM rechazó los códigos TOTP permitidos.",
        PamAuthenticationErrorCode.ConnectionOrCertificateFailure => "No fue posible comunicarse de forma segura con el portal PAM.",
        PamAuthenticationErrorCode.ConfirmationMissing => "El portal PAM no confirmó el inicio de sesión.",
        PamAuthenticationErrorCode.UnexpectedPortalResponse => "El portal PAM devolvió una respuesta de autenticación no reconocida.",
        _ => "La autenticación PAM no pudo completarse.",
    };
}
