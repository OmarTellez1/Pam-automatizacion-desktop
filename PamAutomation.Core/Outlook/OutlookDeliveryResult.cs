namespace PamAutomation.Core.Outlook;

/// <summary>Represents the in-memory outcome of a delivery attempt.</summary>
/// <param name="Status">The delivery confirmation status reported by Outlook.</param>
/// <param name="Message">A safe user-facing description that never includes message content.</param>
public sealed record OutlookDeliveryResult(OutlookDeliveryStatus Status, string Message)
{
    /// <summary>Gets whether external delivery confirmation is available.</summary>
    public bool IsConfirmed => false;
    /// <summary>Creates a result for a request accepted by Outlook but not externally confirmed.</summary>
    public static OutlookDeliveryResult SubmittedUnconfirmed() => new(OutlookDeliveryStatus.SubmittedUnconfirmed, "Outlook recibió la solicitud de envío, pero la entrega no está confirmada.");
    /// <summary>Creates a non-confirmed delivery result without exposing sensitive content.</summary>
    public static OutlookDeliveryResult NotConfirmed() => new(OutlookDeliveryStatus.NotConfirmed, "Outlook no pudo confirmar el envío del mensaje.");
}
