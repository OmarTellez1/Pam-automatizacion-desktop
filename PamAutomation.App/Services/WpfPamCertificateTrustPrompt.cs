using System.Windows;
using System.Windows.Threading;
using PamAutomation.Browser;

namespace PamAutomation.App.Services;

/// <summary>Guides the user through the certificate decision that must remain under their control.</summary>
public sealed class WpfPamCertificateTrustPrompt : IPamCertificateTrustPrompt
{
    /// <inheritdoc />
    public async Task ShowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        static void ShowMessage() => UrgentMessageBox.Show(
                "Microsoft Edge abrió el sitio de PAM, pero requiere que tú confirmes el certificado.\n\n" +
                "1. Ve a la ventana de Edge.\n" +
                "2. Pulsa «Avanzado».\n" +
                "3. Pulsa «Continuar a 10.30.17.117 (no seguro)».\n" +
                "4. Cuando aparezca el campo Username, vuelve aquí y pulsa Aceptar.\n\n" +
                "La aplicación continuará automáticamente con las credenciales y el código temporal.",
                "Confirma el certificado de PAM en Edge",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ShowMessage();
            return;
        }

        await dispatcher.InvokeAsync(
            ShowMessage,
            DispatcherPriority.Normal,
            cancellationToken);
    }
}
