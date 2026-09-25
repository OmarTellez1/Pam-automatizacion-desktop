namespace PamAutomation.Core.Requests;

/// <summary>Identifies the request-form check that failed before a real submission.</summary>
public enum PamRequestFormVerificationStage
{
    /// <summary>The first check immediately after filling the form.</summary>
    AfterFill,

    /// <summary>The final check immediately before clicking Submit Request.</summary>
    BeforeSubmit,
}

/// <summary>Names only request-form fields; it never contains their entered values.</summary>
[Flags]
public enum PamRequestFormMismatchField
{
    /// <summary>No field mismatch was observed.</summary>
    None = 0,
    /// <summary>The start date did not match.</summary>
    StartDate = 1 << 0,
    /// <summary>The start time did not match.</summary>
    StartTime = 1 << 1,
    /// <summary>The duration days did not match.</summary>
    Days = 1 << 2,
    /// <summary>The duration hours did not match.</summary>
    Hours = 1 << 3,
    /// <summary>The duration minutes did not match.</summary>
    Minutes = 1 << 4,
    /// <summary>The reason field did not match; its contents are never captured.</summary>
    Reason = 1 << 5,
    /// <summary>The application-session switch was not enabled.</summary>
    ApplicationSession = 1 << 6,
}

/// <summary>Classifies a transient read failure without retaining portal text.</summary>
public enum PamRequestFormReadFailure
{
    /// <summary>No transient read failed.</summary>
    None,
    /// <summary>A portal read timed out.</summary>
    Timeout,
    /// <summary>A portal read returned an invalid state.</summary>
    InvalidOperation,
    /// <summary>A portal read rejected an argument.</summary>
    Argument,
}

/// <summary>Contains only non-secret form values and the reason's length for diagnosis.</summary>
public sealed record PamRequestFormDiagnosticValues
{
    /// <summary>Initializes the safely bounded set of form fields.</summary>
    /// <param name="startDate">Start date as read or written in the portal.</param>
    /// <param name="startTime">Start time as read or written in the portal.</param>
    /// <param name="days">Duration days.</param>
    /// <param name="hours">Duration hours.</param>
    /// <param name="minutes">Duration minutes.</param>
    /// <param name="reasonLength">Character count only; never the reason text.</param>
    /// <param name="applicationSession">Application-session switch state.</param>
    public PamRequestFormDiagnosticValues(
        string? startDate,
        string? startTime,
        string? days,
        string? hours,
        string? minutes,
        int? reasonLength,
        string? applicationSession)
    {
        if (reasonLength < 0) throw new ArgumentOutOfRangeException(nameof(reasonLength));
        StartDate = startDate;
        StartTime = startTime;
        Days = days;
        Hours = hours;
        Minutes = minutes;
        ReasonLength = reasonLength;
        ApplicationSession = applicationSession;
    }

    /// <summary>Gets the date field.</summary>
    public string? StartDate { get; }

    /// <summary>Gets the time field.</summary>
    public string? StartTime { get; }

    /// <summary>Gets the days field.</summary>
    public string? Days { get; }

    /// <summary>Gets the hours field.</summary>
    public string? Hours { get; }

    /// <summary>Gets the minutes field.</summary>
    public string? Minutes { get; }

    /// <summary>Gets the reason length without retaining its contents.</summary>
    public int? ReasonLength { get; }

    /// <summary>Gets the application-session switch state.</summary>
    public string? ApplicationSession { get; }
}

/// <summary>
/// Carries allowlisted form-verification metadata through submission errors into the explicit diagnostic export.
/// </summary>
public sealed class PamRequestFormVerificationException : InvalidOperationException
{
    /// <summary>Initializes a safe verification failure.</summary>
    /// <param name="stage">Whether verification ran after filling or immediately before submission.</param>
    /// <param name="mismatchFields">Field names observed in the last successful portal read.</param>
    /// <param name="observationCount">Number of verification observations.</param>
    /// <param name="readFailureCount">Number of transient portal-read failures.</param>
    /// <param name="lastReadFailure">Last classified transient portal-read failure.</param>
    /// <param name="expected">Expected non-secret field values.</param>
    /// <param name="observed">Last complete portal observation, if one succeeded.</param>
    public PamRequestFormVerificationException(
        PamRequestFormVerificationStage stage,
        PamRequestFormMismatchField mismatchFields,
        int observationCount,
        int readFailureCount,
        PamRequestFormReadFailure lastReadFailure,
        PamRequestFormDiagnosticValues expected,
        PamRequestFormDiagnosticValues? observed)
        : base(mismatchFields == PamRequestFormMismatchField.None
            ? "El formulario no produjo una lectura estable; el envío permanece bloqueado."
            : $"El formulario no conservó estos campos: {mismatchFields}; el envío permanece bloqueado.")
    {
        const PamRequestFormMismatchField knownFields =
            PamRequestFormMismatchField.StartDate |
            PamRequestFormMismatchField.StartTime |
            PamRequestFormMismatchField.Days |
            PamRequestFormMismatchField.Hours |
            PamRequestFormMismatchField.Minutes |
            PamRequestFormMismatchField.Reason |
            PamRequestFormMismatchField.ApplicationSession;
        if (!Enum.IsDefined(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        if ((mismatchFields & ~knownFields) != 0) throw new ArgumentOutOfRangeException(nameof(mismatchFields));
        if (observationCount <= 0) throw new ArgumentOutOfRangeException(nameof(observationCount));
        if (readFailureCount < 0 || readFailureCount > observationCount)
            throw new ArgumentOutOfRangeException(nameof(readFailureCount));
        if (!Enum.IsDefined(lastReadFailure)) throw new ArgumentOutOfRangeException(nameof(lastReadFailure));
        ArgumentNullException.ThrowIfNull(expected);

        Stage = stage;
        MismatchFields = mismatchFields;
        ObservationCount = observationCount;
        ReadFailureCount = readFailureCount;
        LastReadFailure = lastReadFailure;
        Expected = expected;
        Observed = observed;
    }

    /// <summary>Gets the failed verification stage.</summary>
    public PamRequestFormVerificationStage Stage { get; }

    /// <summary>Gets only the field names observed to differ, never their values.</summary>
    public PamRequestFormMismatchField MismatchFields { get; }

    /// <summary>Gets the number of verification observations.</summary>
    public int ObservationCount { get; }

    /// <summary>Gets the number of transient portal-read failures.</summary>
    public int ReadFailureCount { get; }

    /// <summary>Gets the last safe read-failure category.</summary>
    public PamRequestFormReadFailure LastReadFailure { get; }

    /// <summary>Gets the expected non-secret form fields.</summary>
    public PamRequestFormDiagnosticValues Expected { get; }

    /// <summary>Gets the last complete portal read, or null if none succeeded.</summary>
    public PamRequestFormDiagnosticValues? Observed { get; }
}
