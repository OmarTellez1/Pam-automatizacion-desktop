using System.Windows;
using PamAutomation.App.Services;
using PamAutomation.Core.Execution;

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
        ReuseAvailabilityText.Text = preparation.CanReusePendingRequestIdsForEmail
            ? "Se encontró un conjunto completo y no ambiguo. Puedes reutilizar esos Request ID y enviar el correo sin crear solicitudes nuevas."
            : "Reutilizar IDs no está disponible porque el conjunto no está completo o la acción final no está configurada para enviar correo.";
    }

    public RepeatedPendingDecision Decision { get; private set; } = RepeatedPendingDecision.Cancel;

    private void SubmitAgain_Click(object sender, RoutedEventArgs e)
    {
        Decision = RepeatedPendingDecision.SubmitAgain;
        DialogResult = true;
    }

    private void ReuseExisting_Click(object sender, RoutedEventArgs e)
    {
        if (!ReuseButton.IsEnabled) return;
        Decision = RepeatedPendingDecision.UseExistingRequestIds;
        DialogResult = true;
    }
}
