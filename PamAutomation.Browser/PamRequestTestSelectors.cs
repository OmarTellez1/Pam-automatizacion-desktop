using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Centralizes selectors used exclusively by PAM request test mode.</summary>
internal static partial class PamRequestTestSelectors
{
    public const string Access = "Access";
    public const string SubmitRequest = "Submit Request";
    public const string FormHeading = "Select session start date";
    public const string StartDate = "Start Date";
    public const string StartTime = "Start Time";
    public const string Days = "Days";
    public const string Hours = "Hours";
    public const string Minutes = "Minutes";
    public const string Reason = "Reason";
    public const string ApplicationSession = "Application Session";
    public const string Cancel = "Cancel";
    public const string DiscardChanges = "Discard Changes";
    public const string OpenCalendar = ".flatpickr-calendar.open";
    public const string NextMonth = ".flatpickr-next-month";

    [GeneratedRegex("/api/ps-portal/requests(?:\\?.*)?$", RegexOptions.IgnoreCase)]
    public static partial Regex SubmissionEndpointExpression();
}
