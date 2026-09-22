using PamAutomation.App.Services;
using PamAutomation.Core.Execution;
using System.Windows;

namespace PamAutomation.App.Views;

/// <summary>Lets the user resolve exact requests already present in Pending.</summary>
public partial class RepeatedPendingDecisionWindow : Window
{
    public RepeatedPendingDecisionWindow(PamExecutionPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        InitializeComponent();
        SummaryText.Text =
            $"Pending contiene {preparation.ExactPendingDuplicateCount} solicitud(es) con el mismo System, Account, inicio, expiración y Description.";
        ReuseButton.IsEnabled = preparation.CanReusePendingRequestIdsForEmail;
        ReuseAvailabilityText.Text = BuildAvailabilityMessage(preparation);
        if (!preparation.CanReusePendingRequestIdsForEmail)
        {
            ReuseButton.Visibility = Visibility.Collapsed;
            System.Windows.Controls.Grid.SetColumnSpan(CancelButton, 2);
            CancelButton.Margin = new Thickness(0);
            DecisionGuidanceText.Text =
                "No se creará ni enviará nada. Cancela la automatización y corrige la configuración indicada antes de volver a intentarlo.";
        }
    }

    public RepeatedPendingDecision Decision { get; private set; } = RepeatedPendingDecision.Cancel;

    private void ReuseExisting_Click(object sender, RoutedEventArgs e)
    {
        if (!ReuseButton.IsEnabled) return;
        Decision = RepeatedPendingDecision.UseExistingRequestIds;
        DialogResult = true;
    }

    internal static string BuildAvailabilityMessage(PamExecutionPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        if (preparation.CanReusePendingRequestIdsForEmail)
        {
            return "Todas las solicitudes que intentas crear ya existen en Pending con la misma fecha y hora de inicio, " +
                "duración y motivo. Puedes enviar por correo los Request ID de esas solicitudes existentes sin volver a crearlas. " +
                "También puedes cancelar y cambiar alguno de esos datos que se acaban de mencionar para crear solicitudes nuevas.";
        }

        bool hasCompleteReusableSet =
            preparation.ReusablePendingRequestIds.Count == preparation.FavoriteCount;

        if (hasCompleteReusableSet)
        {
            return "Todas las solicitudes que intentas crear ya existen en Pending, pero el envío de correo con Outlook está desactivado. " +
                "Cancela la automatización y activa «Enviar correo con Outlook» si deseas enviar los Request ID existentes. " +
                "Si necesitas crear solicitudes nuevas, cambia la fecha u hora de inicio, la duración o el motivo.";
        }

        return "Algunas de las solicitudes que intentas crear ya existen en Pending con la misma fecha y hora de inicio, " +
            "duración y motivo. No es posible crear nuevamente esas solicitudes ni enviar un correo incompleto. " +
            "Cancela la automatización y cambia la fecha u hora de inicio, la duración o el motivo antes de volver a intentarlo.";
    }
}
