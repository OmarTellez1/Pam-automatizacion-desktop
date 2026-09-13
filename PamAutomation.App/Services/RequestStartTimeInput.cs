using System.Globalization;

namespace PamAutomation.App.Services;

internal enum RequestStartTimeInputError
{
    None,
    InvalidHour,
    InvalidMinute,
    InvalidMeridiem
}

internal static class RequestStartTimeInput
{
    internal static RequestStartTimeInputError TryParse(
        string? hourText,
        string? minuteText,
        string? meridiemText,
        out TimeOnly startTime)
    {
        startTime = default;

        if (!int.TryParse(hourText, NumberStyles.None, CultureInfo.InvariantCulture, out int hour12) ||
            hour12 is < 1 or > 12)
        {
            return RequestStartTimeInputError.InvalidHour;
        }

        if (!int.TryParse(minuteText, NumberStyles.None, CultureInfo.InvariantCulture, out int minute) ||
            minute is < 0 or > 59)
        {
            return RequestStartTimeInputError.InvalidMinute;
        }

        bool isAm = string.Equals(meridiemText, "AM", StringComparison.Ordinal);
        bool isPm = string.Equals(meridiemText, "PM", StringComparison.Ordinal);
        if (!isAm && !isPm)
        {
            return RequestStartTimeInputError.InvalidMeridiem;
        }

        int hour24 = hour12 % 12 + (isPm ? 12 : 0);
        startTime = new TimeOnly(hour24, minute);
        return RequestStartTimeInputError.None;
    }
}
