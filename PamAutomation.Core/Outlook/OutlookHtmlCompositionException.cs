namespace PamAutomation.Core.Outlook;

/// <summary>Represents a controlled HTML composition failure without exposing message content.</summary>
public sealed class OutlookHtmlCompositionException : Exception
{
    /// <summary>Initializes a composition failure.</summary>
    /// <param name="errorCode">The stable failure category.</param>
    public OutlookHtmlCompositionException(OutlookHtmlCompositionErrorCode errorCode)
        : base(GetMessage(errorCode))
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable failure category.</summary>
    public OutlookHtmlCompositionErrorCode ErrorCode { get; }

    private static string GetMessage(OutlookHtmlCompositionErrorCode errorCode) => errorCode switch
    {
        OutlookHtmlCompositionErrorCode.MissingTemplate => "El contenido del correo es obligatorio.",
        OutlookHtmlCompositionErrorCode.TemplateTooLarge =>
            "El contenido del correo supera el límite permitido.",
        OutlookHtmlCompositionErrorCode.InvalidMarkup =>
            "El contenido enriquecido del correo no tiene un formato válido.",
        OutlookHtmlCompositionErrorCode.MissingRequestIdsMarker =>
            "El contenido debe incluir {{REQUEST_IDS}} exactamente una vez.",
        OutlookHtmlCompositionErrorCode.MultipleRequestIdsMarkers =>
            "El contenido no puede incluir {{REQUEST_IDS}} más de una vez.",
        OutlookHtmlCompositionErrorCode.MissingRequestIds =>
            "No existen Request ID para componer el correo.",
        OutlookHtmlCompositionErrorCode.InvalidRequestId =>
            "Uno o más Request ID no tienen un formato válido.",
        OutlookHtmlCompositionErrorCode.DuplicateRequestId =>
            "Los Request ID del correo no pueden repetirse.",
        OutlookHtmlCompositionErrorCode.UnsafeComposedBody =>
            "El contenido final del correo no superó la validación de seguridad.",
        OutlookHtmlCompositionErrorCode.MissingComposedRequestId =>
            "El contenido final no contiene todos los Request ID esperados.",
        _ => "No fue posible preparar el contenido del correo.",
    };
}
