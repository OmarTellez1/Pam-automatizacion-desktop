namespace PamAutomation.Core.Outlook;

/// <summary>Represents a safe Outlook COM operation failure without exposing COM details.</summary>
public sealed class OutlookOperationException : Exception
{
    /// <summary>Initializes the exception with a stable operation code and preserved cause.</summary>
    /// <param name="errorCode">The failed Outlook operation.</param>
    /// <param name="innerException">The underlying COM or adapter failure.</param>
    public OutlookOperationException(OutlookOperationErrorCode errorCode, Exception innerException)
        : base(GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable Outlook operation failure code.</summary>
    public OutlookOperationErrorCode ErrorCode { get; }

    private static string GetMessage(OutlookOperationErrorCode errorCode) => errorCode switch
    {
        OutlookOperationErrorCode.DuplicateCheckFailed => "No fue posible comprobar correos anteriores de Outlook.",
        OutlookOperationErrorCode.AccountResolutionFailed => "No fue posible consultar las cuentas de Outlook.",
        OutlookOperationErrorCode.AccountValidationFailed => "No fue posible validar la cuenta de Outlook.",
        _ => "No fue posible completar la operación de Outlook.",
    };
}
