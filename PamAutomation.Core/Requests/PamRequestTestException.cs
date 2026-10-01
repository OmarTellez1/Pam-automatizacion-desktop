namespace PamAutomation.Core.Requests;

/// <summary>Represents a controlled and non-sensitive request test failure.</summary>
public sealed class PamRequestTestException : Exception
{
    /// <summary>Initializes a request test failure with a stable code.</summary>
    public PamRequestTestException(PamRequestTestErrorCode errorCode, Exception? innerException = null)
        : base(innerException is PamCalendarSelectionException calendarFailure
            ? calendarFailure.Message : GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable failure category.</summary>
    public PamRequestTestErrorCode ErrorCode { get; }

    private static string GetMessage(PamRequestTestErrorCode errorCode) => errorCode switch
    {
        PamRequestTestErrorCode.InvalidSettings => "La configuración de la solicitud no es válida para el modo de prueba.",
        PamRequestTestErrorCode.PortalUnavailable => "No fue posible validar y descartar todos los formularios de PAM.",
        _ => "No fue posible completar el modo de prueba de solicitudes.",
    };
}
