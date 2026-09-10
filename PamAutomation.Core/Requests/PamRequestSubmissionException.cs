namespace PamAutomation.Core.Requests;

/// <summary>Represents a controlled and non-sensitive real submission failure.</summary>
public sealed class PamRequestSubmissionException : Exception
{
    /// <summary>Initializes a submission failure.</summary>
    /// <param name="errorCode">Stable failure category.</param>
    /// <param name="innerException">Underlying technical failure when available.</param>
    public PamRequestSubmissionException(
        PamRequestSubmissionErrorCode errorCode,
        Exception? innerException = null)
        : base(GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable failure category.</summary>
    public PamRequestSubmissionErrorCode ErrorCode { get; }

    private static string GetMessage(PamRequestSubmissionErrorCode errorCode) => errorCode switch
    {
        PamRequestSubmissionErrorCode.InvalidSettings =>
            "La configuración operativa no es válida para crear solicitudes PAM.",
        PamRequestSubmissionErrorCode.AuthorizationMismatch =>
            "Favorites o la configuración cambió después de la confirmación; no se creó ninguna solicitud.",
        PamRequestSubmissionErrorCode.RequestRejected =>
            "PAM rechazó Submit Request con una respuesta no reconocida.",
        PamRequestSubmissionErrorCode.PortalUnavailable =>
            "No fue posible completar de forma segura las solicitudes PAM.",
        _ => "No fue posible completar las solicitudes PAM.",
    };
}
