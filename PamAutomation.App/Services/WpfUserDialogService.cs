using System.Globalization;
using System.Windows;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Services;

public sealed class WpfUserDialogService : IUserDialogService
{
    public bool ConfirmRealExecution(PamExecutionPreparation preparation, MailSettings? mailSettings)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        string message = string.Join(
            Environment.NewLine,
            "Se crearán solicitudes reales en PAM.",
            string.Empty,
            $"Favorites rows: {preparation.FavoriteCount}",
            $"Inicio: {preparation.Request.StartDate:dd/MM/yyyy} {preparation.Request.StartTime:hh\\:mm}",
            $"Duración: {preparation.Request.SessionHours.ToString(CultureInfo.InvariantCulture)} hora(s)",
            $"Razón: {preparation.Request.RequestReason}",
            $"Acción final: {(preparation.FinalAction == FinalAction.SendEmail ? "Enviar correo" : "Mostrar Request ID")}",
            preparation.FinalAction == FinalAction.SendEmail && mailSettings is not null
                ? $"Destinatarios: {string.Join("; ", mailSettings.ToRecipients)}"
                : string.Empty,
            string.Empty,
            "La ejecución se cancelará si Favorites o la configuración cambian.");
        return MessageBox.Show(message, "Confirmar solicitudes PAM", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmTestEmail(string recipient) => MessageBox.Show(
        $"La validación PAM terminó. ¿Enviar ahora el resultado de prueba únicamente a {recipient}, sin CC?",
        "Confirmar correo de prueba",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning,
        MessageBoxResult.No) == MessageBoxResult.Yes;

    public bool ConfirmOutlookTestEmail(string recipient) => MessageBox.Show(
        $"¿Enviar ahora un correo de comprobación desde y hacia {recipient}, sin CC?",
        "Confirmar prueba de Outlook",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning,
        MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowResult(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) =>
        MessageBox.Show(message, "No se completó la automatización", MessageBoxButton.OK, MessageBoxImage.Error);
}
