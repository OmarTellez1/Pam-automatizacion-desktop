namespace PamAutomation.Core.Outlook;

/// <summary>Represents a safe Outlook COM operation failure without exposing COM details.</summary>
public sealed class OutlookOperationException : Exception
{
    /// <summary>Initializes the exception with a stable operation code and preserved cause.</summary>
    /// <param name="errorCode">The failed Outlook operation.</param>
    /// <param name="innerException">The underlying COM or adapter failure.</param>
    /// <param name="isTransient">Whether this read-only operation can be retried safely.</param>
    public OutlookOperationException(OutlookOperationErrorCode errorCode, Exception innerException, bool isTransient = false)
        : base(GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    /// <summary>Gets the stable Outlook operation failure code.</summary>
    public OutlookOperationErrorCode ErrorCode { get; }

    /// <summary>Gets whether the adapter recognized a recoverable read failure.</summary>
    public bool IsTransient { get; }

    private static string GetMessage(OutlookOperationErrorCode errorCode) => errorCode switch
    {
        OutlookOperationErrorCode.ApplicationUnavailable =>
            "Outlook clásico está instalado, pero Windows no pudo conectarse con la aplicación.",
        OutlookOperationErrorCode.DuplicateCheckFailed => "No fue posible comprobar correos anteriores de Outlook.",
        OutlookOperationErrorCode.DeliveryObservationFailed => "No fue posible comprobar el estado del correo enviado a Outlook.",
        OutlookOperationErrorCode.AccountResolutionFailed => "No fue posible consultar las cuentas de Outlook.",
        OutlookOperationErrorCode.AccountValidationFailed => "No fue posible validar la cuenta de Outlook.",
        _ => "No fue posible completar la operación de Outlook.",
    };
}
