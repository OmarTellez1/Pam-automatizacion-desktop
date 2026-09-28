namespace PamAutomation.Core.Outlook;

/// <summary>Submission may have reached Outlook; observing is safe, resending is not.</summary>
public sealed class OutlookDeliveryUncertainException(Exception innerException)
    : Exception("No se pudo confirmar el estado del correo. Comprueba el envío existente; no lo vuelvas a enviar.", innerException);
