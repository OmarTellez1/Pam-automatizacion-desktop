namespace PamAutomation.Core.Outlook;

/// <summary>Describes whether Outlook confirmed acceptance of a message for delivery.</summary>
public enum OutlookDeliveryStatus
{
    /// <summary>The newly sent message was found in Sent Items.</summary>
    SentConfirmed,
    /// <summary>The newly sent message remained in Outbox and was moved to Drafts.</summary>
    OutboxMovedToDraft,
    /// <summary>The user declined the final test-email confirmation.</summary>
    SkippedByUser,
    /// <summary>The current application session already attempted the identical message.</summary>
    DuplicateInMemory,
    /// <summary>No message was created because the run produced no new Request ID.</summary>
    SkippedNoRequestIds,
    /// <summary>An identical app-generated message already exists in Sent Items.</summary>
    DuplicateSentConfirmed,
    /// <summary>An identical app-generated message already exists in Drafts.</summary>
    DuplicateDraft,
    /// <summary>An identical message was found in Outbox and moved to Drafts.</summary>
    DuplicateOutboxMovedToDraft,
    /// <summary>Outlook received the send request, but delivery is not externally confirmed.</summary>
    SubmittedUnconfirmed,
    /// <summary>Outlook could not confirm that it accepted the message.</summary>
    NotConfirmed,
}
