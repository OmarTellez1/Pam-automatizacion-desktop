namespace PamAutomation.Core.Outlook;

/// <summary>Identifies a safe failure while invoking Outlook classic COM.</summary>
public enum OutlookOperationErrorCode
{
    /// <summary>Outlook classic was registered but did not respond to controlled COM activation.</summary>
    ApplicationUnavailable,

    /// <summary>Outlook could not inspect previous messages safely.</summary>
    DuplicateCheckFailed,
    /// <summary>Outlook could not resolve the configured account.</summary>
    AccountResolutionFailed,

    /// <summary>Outlook could not validate the configured account.</summary>
    AccountValidationFailed,

    /// <summary>Outlook could not observe a submitted message safely.</summary>
    DeliveryObservationFailed,
}
