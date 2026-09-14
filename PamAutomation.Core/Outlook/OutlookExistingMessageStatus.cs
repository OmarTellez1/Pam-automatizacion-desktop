namespace PamAutomation.Core.Outlook;

public enum OutlookExistingMessageStatus
{
    None,
    Sent,
    Draft,
    /// <summary>The message is still waiting for Outlook to transmit it.</summary>
    Outbox,
}
