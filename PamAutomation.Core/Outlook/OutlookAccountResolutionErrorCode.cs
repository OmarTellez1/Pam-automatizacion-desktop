namespace PamAutomation.Core.Outlook;

/// <summary>Identifies why a configured Outlook account could not be resolved exactly.</summary>
public enum OutlookAccountResolutionErrorCode
{
    /// <summary>No Outlook account has the requested SMTP address.</summary>
    AccountNotFound,
    /// <summary>More than one Outlook account has the requested SMTP address.</summary>
    MultipleAccountsFound,
}
