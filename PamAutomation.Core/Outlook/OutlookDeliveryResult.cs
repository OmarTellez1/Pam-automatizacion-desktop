namespace PamAutomation.Core.Outlook;

/// <summary>Represents the in-memory outcome of a delivery attempt.</summary>
/// <param name="Status">The delivery confirmation status reported by Outlook.</param>
/// <param name="Message">A safe user-facing description that never includes message content.</param>
public sealed record OutlookDeliveryResult(OutlookDeliveryStatus Status, string Message)
{
    /// <summary>Gets whether external delivery confirmation is available.</summary>
    public bool IsConfirmed => Status is OutlookDeliveryStatus.SentConfirmed or OutlookDeliveryStatus.DuplicateSentConfirmed;
    public static OutlookDeliveryResult SentConfirmed() => new(OutlookDeliveryStatus.SentConfirmed, "El correo fue localizado en Enviados.");
    public static OutlookDeliveryResult OutboxPending() => new(OutlookDeliveryStatus.OutboxPending, "Outlook recibió el correo y todavía lo mantiene en Bandeja de salida. No se movió ni se duplicó el mensaje; Outlook continuará procesándolo.");
    public static OutlookDeliveryResult DraftedNotSent() => new(OutlookDeliveryStatus.DraftedNotSent, "Outlook dejó el correo en Borradores y no confirmó el envío. Puedes revisarlo allí sin volver a ejecutar la automatización.");
    public static OutlookDeliveryResult SkippedByUser() => new(OutlookDeliveryStatus.SkippedByUser, "El correo fue cancelado por el usuario; no se creó ningún mensaje.");
    public static OutlookDeliveryResult DuplicateInMemory() => new(OutlookDeliveryStatus.DuplicateInMemory, "Esta sesión ya intentó el mismo correo; no se creó un duplicado.");
    public static OutlookDeliveryResult SkippedNoRequestIds() => new(OutlookDeliveryStatus.SkippedNoRequestIds, "No se creó un correo porque la ejecución no produjo Request ID nuevos.");
    public static OutlookDeliveryResult DuplicateSentConfirmed() => new(OutlookDeliveryStatus.DuplicateSentConfirmed, "Ya existe un correo idéntico en Enviados; no se creó un duplicado.");
    public static OutlookDeliveryResult DuplicateDraft() => new(OutlookDeliveryStatus.DuplicateDraft, "Ya existe un correo idéntico en Borradores; no se creó un duplicado.");
    public static OutlookDeliveryResult DuplicateOutboxPending() => new(OutlookDeliveryStatus.DuplicateOutboxPending, "Ya existe un correo idéntico en Bandeja de salida; no se creó otro y Outlook continuará procesando el existente.");
    /// <summary>Creates a result for a send request accepted by Outlook.</summary>
    public static OutlookDeliveryResult SubmittedUnconfirmed() => new(OutlookDeliveryStatus.SubmittedUnconfirmed, "El correo se envió correctamente desde Outlook.");
    /// <summary>Creates a non-confirmed delivery result without exposing sensitive content.</summary>
    public static OutlookDeliveryResult NotConfirmed() => new(OutlookDeliveryStatus.NotConfirmed, "Outlook no pudo confirmar el envío del mensaje.");
}
