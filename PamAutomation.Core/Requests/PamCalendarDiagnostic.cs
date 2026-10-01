namespace PamAutomation.Core.Requests;

/// <summary>Subcause of a calendar observation; never includes arbitrary portal text.</summary>
public enum PamCalendarReadCondition
{
    Valid, CalendarNotVisible, MultipleVisibleCalendars, HeaderIncomplete, HeaderAmbiguous,
    MonthValueInvalid, YearValueInvalid, GridEmpty, DayLabelInvalid, HeaderGridMismatch,
    FallbackGridIncomplete, CalendarReadTimeout, CalendarReadFailed, TargetDayMissing,
    TargetDayDisabled, SelectionReverted, SelectionMismatch, UnexpectedMonth
}

/// <summary>Calendar stages used in the bounded diagnostic history.</summary>
public enum PamCalendarStage { ConfirmInitialDate, Open, ReadInitialMonth, SelectMonth, WaitTransition, SelectDay, ConfirmDate }

/// <summary>One normalized calendar observation with no DOM, reason, identity or secrets.</summary>
public sealed record PamCalendarObservation(PamCalendarStage Stage, PamCalendarReadCondition Condition,
    long ElapsedMilliseconds, long RemainingMilliseconds, int? CalendarCount, int? MonthControlCount,
    int? YearControlCount, int? DayCount, DateOnly? HeaderMonth, DateOnly? GridMonth, bool NativeHeader);

/// <summary>Bounded summary attached to a failure and exported only by explicit diagnostic export.</summary>
public sealed record PamCalendarDiagnostic(Guid? AttemptId, int FormOrdinal, int ExpectedCount,
    int ObservationCount, int ReadFailureCount, int ConsecutiveMatches,
    IReadOnlyList<PamCalendarObservation> History);
