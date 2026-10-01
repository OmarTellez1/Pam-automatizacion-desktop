namespace PamAutomation.Core.Requests;

/// <summary>Represents a controlled and non-sensitive real submission failure.</summary>
public sealed class PamRequestSubmissionException : Exception
{
    /// <summary>Initializes a submission failure.</summary>
    /// <param name="errorCode">Stable failure category.</param>
    /// <param name="innerException">Underlying technical failure when available.</param>
    public PamRequestSubmissionException(
        PamRequestSubmissionErrorCode errorCode,
        Exception? innerException = null,
        int processedCount = 0,
        PamRequestRunResult? partialRun = null,
        bool? submitMayHaveOccurred = null,
        IReadOnlyList<string>? recoveredRequestIds = null,
        string? recoveryStatus = null,
        PamRequestPortalOutcome? confirmedOutcome = null,
        bool isKnownRejection = false)
        : base(innerException is PamCalendarSelectionException calendarFailure
            ? calendarFailure.Message : GetMessage(errorCode), innerException)
    {
        if (processedCount < 0) throw new ArgumentOutOfRangeException(nameof(processedCount));
        ErrorCode = errorCode;
        ProcessedCount = processedCount;
        PartialRun = partialRun;
        SubmitMayHaveOccurred = submitMayHaveOccurred;
        RecoveredRequestIds = Array.AsReadOnly(recoveredRequestIds?.ToArray() ?? []);
        RecoveryStatus = recoveryStatus;
        ConfirmedOutcome = confirmedOutcome;
        IsKnownRejection = isKnownRejection;
    }

    /// <summary>Gets the stable failure category.</summary>
    public PamRequestSubmissionErrorCode ErrorCode { get; }

    /// <summary>Gets how many rows reached a known terminal outcome before the failure.</summary>
    public int ProcessedCount { get; }
    public PamRequestRunResult? PartialRun { get; }
    public bool? SubmitMayHaveOccurred { get; }
    public IReadOnlyList<string> RecoveredRequestIds { get; }
    public string? RecoveryStatus { get; }
    public PamRequestPortalOutcome? ConfirmedOutcome { get; }
    public bool IsKnownRejection { get; }
    public PamSubmissionEvidence? Evidence { get; init; }

    private static string GetMessage(PamRequestSubmissionErrorCode errorCode) => errorCode switch
    {
        PamRequestSubmissionErrorCode.InvalidSettings =>
            "La configuración operativa no es válida para crear solicitudes PAM.",
        PamRequestSubmissionErrorCode.AuthorizationMismatch =>
            "Favorites o la configuración cambió después de la confirmación; no se creó ninguna solicitud.",
        PamRequestSubmissionErrorCode.RequestRejected =>
            "Submit Request no produjo un resultado reconocido. Revisa Pending antes de reintentar.",
        PamRequestSubmissionErrorCode.Canceled => "La ejecución fue cancelada; se conservan los resultados conocidos.",
        PamRequestSubmissionErrorCode.PortalUnavailable =>
            "No fue posible completar de forma segura las solicitudes PAM.",
        PamRequestSubmissionErrorCode.ReconciliationFailedAfterSubmission =>
            "Pueden existir solicitudes creadas, pero no fue posible reconciliar todos los resultados en Pending. Se conservan los resultados conocidos. Revisa Pending antes de volver a solicitar.",
        _ => "No fue posible completar las solicitudes PAM.",
    };
}

/// <summary>Technical boundary evidence; observing a POST does not prove acceptance.</summary>
public sealed record PamSubmissionEvidence(bool ClickAttempted, bool PostObserved, bool ResponseObserved);
