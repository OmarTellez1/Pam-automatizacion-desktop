namespace PamAutomation.Core.Outlook;

/// <summary>Describes whether Outlook confirmed acceptance of a message for delivery.</summary>
public enum OutlookDeliveryStatus
{
    /// <summary>Outlook received the send request, but delivery is not externally confirmed.</summary>
    SubmittedUnconfirmed,
    /// <summary>Outlook could not confirm that it accepted the message.</summary>
    NotConfirmed,
}
