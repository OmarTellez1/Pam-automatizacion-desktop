namespace PamAutomation.Core.Requests;

/// <summary>Represents a controlled, non-sensitive <c>Pending</c> failure.</summary>
public sealed class PamPendingException : Exception
{
    /// <summary>Initializes the failure.</summary>
    public PamPendingException(PamPendingErrorCode errorCode, Exception? innerException = null)
        : base(GetMessage(errorCode), innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Gets the stable failure category.</summary>
    public PamPendingErrorCode ErrorCode { get; }

    private static string GetMessage(PamPendingErrorCode errorCode) => errorCode switch
    {
        PamPendingErrorCode.InvalidRequest => "Pending contiene una solicitud incompleta o inválida.",
        PamPendingErrorCode.DuplicateRequestId => "Pending contiene Request ID duplicados.",
        PamPendingErrorCode.AmbiguousIdentity =>
            "Pending no permite asociar de forma única todas las Favorites rows esperadas.",
        PamPendingErrorCode.FieldMismatch =>
            "Pending no coincide con la expiración, Description o Application solicitadas.",
        _ => "No fue posible leer o comparar Pending de forma segura.",
    };
}
