using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly RequestIdAccumulator accumulator;
    private MailSettings? accumulatedMailSnapshot;
    public IRelayCommand ToggleAccumulationCommand { get; private set; } = null!;
    public IAsyncRelayCommand SendAccumulatedMailCommand { get; private set; } = null!;
    public IRelayCommand DiscardAccumulatedCommand { get; private set; } = null!;
    public bool IsAccumulating => accumulator.IsEnabled;
    public int AccumulatedCount => accumulator.Count;
    public string AccumulatedCountText => $"{AccumulatedCount} solicitudes acumuladas";
    public bool CanChangeFinalAction => AreControlsEnabled && !IsAccumulating;
    public bool CanToggleAccumulation => AreControlsEnabled && AccumulatedCount == 0;
    public bool CanSendAccumulatedMail => AreControlsEnabled && AccumulatedCount > 0 && executionService is not null;
    public bool CanDiscardAccumulated => AreControlsEnabled && AccumulatedCount > 0;
    public string AccumulatedMailActionText => accumulator.MailState == AccumulatedMailState.AwaitingConfirmation
        ? "Comprobar envío" : "Enviar correo";
    public string AccumulationNotice => accumulator.MailState == AccumulatedMailState.AwaitingConfirmation
        ? "Envío sin confirmar. Los IDs se conservan; solo se comprobará el correo existente, sin repetir el envío."
        : AccumulatedCount > 0 ? RequestIdAccumulator.CollectionLockMessage : string.Empty;

    private void InitializeAccumulationCommands()
    {
        ToggleAccumulationCommand = new RelayCommand(() =>
        {
            if (!CanToggleAccumulation) return;
            accumulator.SetEnabled(!IsAccumulating);
            RefreshPresentationState();
        }, () => CanToggleAccumulation);
        SendAccumulatedMailCommand = new AsyncRelayCommand(SendAccumulatedMailAsync, () => CanSendAccumulatedMail);
        DiscardAccumulatedCommand = new RelayCommand(() =>
        {
            if (!CanDiscardAccumulated || dialogs?.ConfirmDiscardAccumulated(AccumulatedCount) != true) return;
            accumulator.Discard();
            accumulatedMailSnapshot = null;
            RefreshPresentationState();
        }, () => CanDiscardAccumulated);
    }

    private async Task SendAccumulatedMailAsync()
    {
        if (!CanSendAccumulatedMail || dialogs is null) return;
        bool observing = accumulator.MailState == AccumulatedMailState.AwaitingConfirmation;
        bool started = false;
        IsRunning = true;
        try
        {
            if (!observing)
            {
                await ReloadFavoriteCollectionsAsync();
                if (favoriteCollectionsState.ActiveCollection?.Id != accumulator.CollectionId)
                    throw new InvalidOperationException(RequestIdAccumulator.CollectionLockMessage);
                if (outlookAccountSettingsStore is not null)
                    outlookSmtpAddress = (await outlookAccountSettingsStore.LoadAsync())?.SmtpAddress ?? string.Empty;
                if (mailSignatureStore is not null)
                {
                    mailSignature = await mailSignatureStore.LoadAsync();
                    signatureLoadError = string.Empty;
                }
                RefreshEffectiveMail();
                if (!IsMailConfigured) throw new InvalidOperationException(mailConfigurationError);
                MailSettings snapshot = mailSettings! with
                {
                    ToRecipients = mailSettings!.ToRecipients.ToArray(),
                    CcRecipients = mailSettings.CcRecipients.ToArray(),
                    InlineImages = mailSettings.InlineImages.ToArray()
                };
                if (!dialogs.ConfirmAccumulatedEmail(favoriteCollectionsState.ActiveCollection!.Name, AccumulatedCount, snapshot))
                    return;
                accumulatedMailSnapshot = snapshot;
            }
            if (accumulatedMailSnapshot is null) throw new InvalidOperationException("No está disponible la referencia al correo pendiente.");
            IReadOnlyList<string> ids = accumulator.BeginMailOperation(observing);
            started = true;
            RefreshPresentationState();
            StatusMessage = observing ? "Comprobando el correo existente en Outlook…" : "Enviando el correo acumulado…";
            OutlookDeliveryResult delivery = observing
                ? await executionService!.ObserveResultEmailAsync(accumulatedMailSnapshot, ids)
                : await executionService!.SendAccumulatedEmailAsync(accumulatedMailSnapshot, ids);
            bool uncertain = !delivery.IsConfirmed && delivery.Status is not
                (OutlookDeliveryStatus.SkippedByUser or OutlookDeliveryStatus.SkippedNoRequestIds);
            accumulator.CompleteMailOperation(delivery.IsConfirmed, uncertain);
            started = false;
            if (delivery.IsConfirmed)
            {
                accumulatedMailSnapshot = null;
                StatusMessage = "Correo localizado en Enviados. Acumulado vaciado.";
                dialogs.ShowResult("Correo confirmado en Enviados",
                    "El correo fue localizado en Enviados y el acumulado se limpió. Esto no confirma la recepción por los destinatarios.");
            }
            else
            {
                StatusMessage = uncertain ? AccumulationNotice : "El acumulado se conserva. No se creó un correo.";
                dialogs.ShowResult("Acumulado conservado", uncertain
                    ? "No se ha confirmado el correo en Enviados. Los IDs siguen acumulados. Puedes comprobar su estado; no se enviará otro correo. Descartar el acumulado no elimina el correo de Outlook."
                    : StatusMessage);
            }
        }
        catch (Exception exception)
        {
            bool uncertain = observing || exception is OutlookDeliveryUncertainException;
            logger.LogWarning(exception, "Accumulated mail operation failed. ObservationOnly={ObservationOnly}, Uncertain={Uncertain}.", observing, uncertain);
            if (started) accumulator.CompleteMailOperation(false, uncertain);
            if (!uncertain) accumulatedMailSnapshot = null;
            StatusMessage = uncertain
                ? "No se pudo confirmar el correo. Conservamos los IDs; usa Comprobar envío, sin reenviar."
                : "No se inició el envío. Conservamos los IDs; corrige la configuración o el problema e inténtalo nuevamente.";
            dialogs.ShowError(uncertain ? StatusMessage : StatusMessage + " " + exception.Message);
        }
        finally
        {
            IsRunning = false;
            RefreshPresentationState();
        }
    }

    /// <summary>Normal close is explicit; Windows session termination never waits for a modal decision.</summary>
    public bool TryClose(bool systemShutdown = false)
    {
        if (systemShutdown) return true;
        if (IsAccumulating && IsRunning)
        {
            dialogs?.ShowError("Espera a que termine la operación antes de cerrar la aplicación.");
            return false;
        }
        if (AccumulatedCount == 0) return true;
        if (dialogs?.ConfirmCloseAccumulated(AccumulatedCount) != true) return false;
        accumulator.Discard();
        accumulatedMailSnapshot = null;
        RefreshPresentationState();
        return true;
    }

    private void RefreshAccumulationPresentation()
    {
        OnPropertyChanged(nameof(IsAccumulating));
        OnPropertyChanged(nameof(AccumulatedCount));
        OnPropertyChanged(nameof(AccumulatedCountText));
        OnPropertyChanged(nameof(CanChangeFinalAction));
        OnPropertyChanged(nameof(CanToggleAccumulation));
        OnPropertyChanged(nameof(CanSendAccumulatedMail));
        OnPropertyChanged(nameof(CanDiscardAccumulated));
        OnPropertyChanged(nameof(AccumulatedMailActionText));
        OnPropertyChanged(nameof(AccumulationNotice));
        ToggleAccumulationCommand?.NotifyCanExecuteChanged();
        SendAccumulatedMailCommand?.NotifyCanExecuteChanged();
        DiscardAccumulatedCommand?.NotifyCanExecuteChanged();
    }
}
