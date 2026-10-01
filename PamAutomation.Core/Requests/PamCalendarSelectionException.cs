namespace PamAutomation.Core.Requests;

/// <summary>Identifies the failed calendar operation without recording portal text.</summary>
public enum PamCalendarFailure
{
    OpenFailed,
    AmbiguousCalendar,
    MonthUnreadable,
    NavigationUnavailable,
    MonthTransitionTimeout,
    DateUnavailable,
    AmbiguousDate,
    SelectionNotRetained,
    NavigationLimit,
    Timeout,
    PortalReadFailed,
}

/// <summary>Safe calendar metadata retained through submission/test wrappers and diagnostic export.</summary>
public sealed class PamCalendarSelectionException : InvalidOperationException
{
    /// <summary>Initializes a classified failure with only dates and navigation counts.</summary>
    /// <param name="failure">Stable failure category.</param>
    /// <param name="targetDate">Requested start date.</param>
    /// <param name="observedMonth">Last readable calendar month, or null.</param>
    /// <param name="navigationCount">Number of month-navigation clicks.</param>
    /// <param name="innerException">Underlying browser failure, if available.</param>
    public PamCalendarSelectionException(PamCalendarFailure failure, DateOnly targetDate,
        DateOnly? observedMonth, int navigationCount, Exception? innerException = null)
        : base("No fue posible seleccionar y confirmar la fecha en el calendario PAM. No se envió el formulario afectado.", innerException)
    {
        if (!Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(failure));
        if (navigationCount < 0) throw new ArgumentOutOfRangeException(nameof(navigationCount));
        Failure = failure;
        TargetDate = targetDate;
        ObservedMonth = observedMonth;
        NavigationCount = navigationCount;
    }

    /// <summary>Gets the stable failure category.</summary>
    public PamCalendarFailure Failure { get; }
    /// <summary>Gets the requested date.</summary>
    public DateOnly TargetDate { get; }
    /// <summary>Gets the last observed month.</summary>
    public DateOnly? ObservedMonth { get; }
    /// <summary>Gets how many navigation clicks were issued.</summary>
    public int NavigationCount { get; }
}
