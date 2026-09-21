using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.App.Views;

namespace PamAutomation.App.Services;

public sealed class WpfUserDialogService : IUserDialogService
{
    public RepeatedPendingDecision ChooseRepeatedPendingAction(PamExecutionPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        Window? owner = Application.Current?.MainWindow;
        if (owner?.WindowState == WindowState.Minimized)
        {
            owner.WindowState = WindowState.Normal;
        }

        owner?.Activate();
        var window = new RepeatedPendingDecisionWindow(preparation)
        {
            Owner = owner,
            Topmost = true,
        };
        window.ShowDialog();
        return window.Decision;
    }

    public bool ConfirmAutomationStart(AppSettings settings, MailSettings? mailSettings, string? collectionName = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string message = FormatAutomationStartMessage(settings, mailSettings, collectionName);
        return UrgentMessageBox.Show(message, "Iniciar automatización PAM", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    internal static string FormatAutomationStartMessage(
        AppSettings settings,
        MailSettings? mailSettings,
        string? collectionName = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequestSettings request = settings.Request;
        string startTime = request.StartTime.ToString(
            "hh:mm tt",
            CultureInfo.GetCultureInfo("en-US"));
        string toRecipients = mailSettings is not null && mailSettings.ToRecipients.Count > 0
            ? string.Join("; ", mailSettings.ToRecipients)
            : "Ninguno";
        string ccRecipients = mailSettings is not null && mailSettings.CcRecipients.Count > 0
            ? string.Join("; ", mailSettings.CcRecipients)
            : "Ninguno";

        return string.Join(
            Environment.NewLine,
            "¿Deseas iniciar la automatización y crear solicitudes reales en PAM?",
            string.Empty,
            string.IsNullOrWhiteSpace(collectionName)
                ? "Alcance: Todos los Favorites"
                : $"Colección: {collectionName}",
            $"Inicio: {request.StartDate:dd/MM/yyyy} {startTime}",
            $"Duración: {request.SessionHours.ToString(CultureInfo.InvariantCulture)} hora(s)",
            $"Razón: {request.RequestReason}",
            $"Acción final: {(settings.FinalAction == FinalAction.SendEmail ? "Enviar correo" : "Mostrar Request ID")}",
            settings.FinalAction == FinalAction.SendEmail && mailSettings is not null
                ? $"Para: {toRecipients}"
                : string.Empty,
            settings.FinalAction == FinalAction.SendEmail && mailSettings is not null
                ? $"CC: {ccRecipients}"
                : string.Empty,
            string.Empty,
            "Después de confirmar, las validaciones se ejecutarán en segundo plano.",
            "Solo se solicitará otra decisión si existen solicitudes idénticas en Pending.");
    }

    public bool ConfirmTestEmail(string recipient) => UrgentMessageBox.Show(
        $"La validación PAM terminó. ¿Enviar ahora el resultado de prueba únicamente a {recipient}, sin CC?",
        "Confirmar correo de prueba",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning,
        MessageBoxResult.No) == MessageBoxResult.Yes;

    public bool ConfirmOutlookTestEmail(string recipient) => UrgentMessageBox.Show(
        $"¿Enviar ahora un correo de comprobación desde y hacia {recipient}, sin CC?",
        "Confirmar prueba de Outlook",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning,
        MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowResult(string title, string message) =>
        UrgentMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) =>
        UrgentMessageBox.Show(message, "No se completó la automatización", MessageBoxButton.OK, MessageBoxImage.Error);

    public string? SelectDiagnosticExportPath(string suggestedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        var dialog = new SaveFileDialog
        {
            Title = "Exportar diagnóstico de Pam Automation",
            FileName = suggestedFileName,
            DefaultExt = ".zip",
            Filter = "Paquete de diagnóstico (*.zip)|*.zip",
            AddExtension = true,
            OverwritePrompt = true,
            CheckPathExists = true
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }
}
