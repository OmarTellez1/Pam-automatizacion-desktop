namespace PamAutomation.Core.Outlook;

/// <summary>Represents the in-memory outcome of a delivery attempt.</summary>
/// <param name="Status">The delivery confirmation status reported by Outlook.</param>
/// <param name="Message">A safe user-facing description that never includes message content.</param>
public sealed record OutlookDeliveryResult(OutlookDeliveryStatus Status, string Message)
{
    /// <summary>Gets whether external delivery confirmation is available.</summary>
    public bool IsConfirmed => Status is OutlookDeliveryStatus.SentConfirmed or OutlookDeliveryStatus.DuplicateSentConfirmed;
    public static OutlookDeliveryResult SentConfirmed() => new(OutlookDeliveryStatus.SentConfirmed, "El correo fue localizado en Enviados.");
    public static OutlookDeliveryResult OutboxMovedToDraft() => new(OutlookDeliveryStatus.OutboxMovedToDraft, "El correo permanecía en Bandeja de salida y se movió a Borradores; el envío no está confirmado.");
    public static OutlookDeliveryResult SkippedByUser() => new(OutlookDeliveryStatus.SkippedByUser, "El correo fue cancelado por el usuario; no se creó ningún mensaje.");
    public static OutlookDeliveryResult DuplicateInMemory() => new(OutlookDeliveryStatus.DuplicateInMemory, "Esta sesión ya intentó el mismo correo; no se creó un duplicado.");
    public static OutlookDeliveryResult SkippedNoRequestIds() => new(OutlookDeliveryStatus.SkippedNoRequestIds, "No se creó un correo porque la ejecución no produjo Request ID nuevos.");
    public static OutlookDeliveryResult DuplicateSentConfirmed() => new(OutlookDeliveryStatus.DuplicateSentConfirmed, "Ya existe un correo idéntico en Enviados; no se creó un duplicado.");
    public static OutlookDeliveryResult DuplicateDraft() => new(OutlookDeliveryStatus.DuplicateDraft, "Ya existe un correo idéntico en Borradores; no se creó un duplicado.");
    public static OutlookDeliveryResult DuplicateOutboxMovedToDraft() => new(OutlookDeliveryStatus.DuplicateOutboxMovedToDraft, "Se encontró un correo idéntico en Bandeja de salida y se movió a Borradores; el envío no está confirmado.");
    /// <summary>Creates a result for a request accepted by Outlook but not externally confirmed.</summary>
    public static OutlookDeliveryResult SubmittedUnconfirmed() => new(OutlookDeliveryStatus.SubmittedUnconfirmed, "Outlook recibió la solicitud de envío, pero la entrega no está confirmada.");
    /// <summary>Creates a non-confirmed delivery result without exposing sensitive content.</summary>
    public static OutlookDeliveryResult NotConfirmed() => new(OutlookDeliveryStatus.NotConfirmed, "Outlook no pudo confirmar el envío del mensaje.");
}
