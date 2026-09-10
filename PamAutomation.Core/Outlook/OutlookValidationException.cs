namespace PamAutomation.Core.Outlook;

/// <summary>Represents a safe validation failure before an Outlook operation starts.</summary>
public sealed class OutlookValidationException : Exception
{
    /// <summary>Initializes the exception with a stable validation code.</summary>
    /// <param name="errorCode">The validation failure code.</param>
    /// <param name="innerException">The optional safe underlying validation failure.</param>
    public OutlookValidationException(OutlookValidationErrorCode errorCode, Exception? innerException = null)
        : base(GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable validation failure code.</summary>
    public OutlookValidationErrorCode ErrorCode { get; }

    private static string GetMessage(OutlookValidationErrorCode errorCode) => errorCode switch
    {
        OutlookValidationErrorCode.InvalidSmtpAddress => "La cuenta de Outlook configurada no es válida.",
        OutlookValidationErrorCode.MissingSubject => "El asunto del correo es obligatorio.",
        OutlookValidationErrorCode.MissingHtmlBody => "El contenido del correo es obligatorio.",
        OutlookValidationErrorCode.InvalidRecipient => "Uno o más destinatarios de correo no son válidos.",
        OutlookValidationErrorCode.DuplicateRecipient => "Un destinatario no puede repetirse entre Para y CC.",
        OutlookValidationErrorCode.MissingRequestId => "El correo no contiene todos los identificadores de solicitud esperados.",
        OutlookValidationErrorCode.UnsafeHtmlBody => "El contenido HTML del correo no es seguro o no está saneado.",
        _ => "La configuración del correo no es válida.",
    };
}
