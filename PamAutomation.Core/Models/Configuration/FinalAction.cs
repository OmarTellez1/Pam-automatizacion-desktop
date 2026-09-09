namespace PamAutomation.Core.Models.Configuration;

/// <summary>
/// Defines how the application presents the result after a PAM execution.
/// </summary>
public enum FinalAction
{
    /// <summary>
    /// Displays the request identifiers in the application.
    /// </summary>
    DisplayRequestIds = 0,

    /// <summary>
    /// Sends the result through the configured Outlook account.
    /// </summary>
    SendEmail = 1
}
