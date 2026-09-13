namespace PamAutomation.Core.Outlook;

/// <summary>Identifies a safe failure while invoking Outlook classic COM.</summary>
public enum OutlookOperationErrorCode
{
    DuplicateCheckFailed,
    /// <summary>Outlook could not resolve the configured account.</summary>
    AccountResolutionFailed,

    /// <summary>Outlook could not validate the configured account.</summary>
    AccountValidationFailed,
}
