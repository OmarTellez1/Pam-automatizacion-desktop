namespace PamAutomation.Core.Outlook;

/// <summary>Represents an unambiguous, safe account-resolution failure.</summary>
public sealed class OutlookAccountResolutionException : Exception
{
    /// <summary>Initializes the exception with a stable resolution code.</summary>
    /// <param name="errorCode">The account resolution failure code.</param>
    public OutlookAccountResolutionException(OutlookAccountResolutionErrorCode errorCode) : base(errorCode == OutlookAccountResolutionErrorCode.AccountNotFound ? "No se encontró la cuenta de Outlook configurada." : "La cuenta de Outlook configurada no es única.") => ErrorCode = errorCode;
    /// <summary>Gets the stable account resolution failure code.</summary>
    public OutlookAccountResolutionErrorCode ErrorCode { get; }
}
