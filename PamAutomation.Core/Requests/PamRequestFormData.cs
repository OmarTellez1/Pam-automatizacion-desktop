using System.Diagnostics;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Requests;

/// <summary>Contains the exact values written to one PAM request form.</summary>
[DebuggerDisplay("PAM request form data (redacted)")]
public sealed record PamRequestFormData
{
    /// <summary>Initializes validated form values.</summary>
    /// <param name="startDate">Session start date in Ecuador.</param>
    /// <param name="startTime">Session start time.</param>
    /// <param name="durationDays">Whole duration days; phase 12 requires zero.</param>
    /// <param name="durationHours">Whole duration hours.</param>
    /// <param name="durationMinutes">Additional duration minutes.</param>
    /// <param name="reason">Business reason written to PAM.</param>
    public PamRequestFormData(
        DateOnly startDate,
        TimeOnly startTime,
        int durationDays,
        int durationHours,
        int durationMinutes,
        string reason)
    {
        if (durationDays != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationDays), "Days debe permanecer en cero.");
        }

        if (durationHours is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(durationHours), "Hours debe estar entre 0 y 8.");
        }

        if (durationMinutes is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Minutes debe estar entre 0 y 59.");
        }

        int totalMinutes = checked((durationHours * 60) + durationMinutes);
        if (totalMinutes is < 1 or > 480)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                "La duración total debe estar entre 1 minuto y 8 horas.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason es obligatorio.", nameof(reason));
        }

        StartDate = startDate;
        StartTime = startTime;
        DurationDays = durationDays;
        DurationHours = durationHours;
        DurationMinutes = durationMinutes;
        Reason = reason.Trim();
    }

    /// <summary>Gets the session start date in Ecuador.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Gets the session start time.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Gets the value for the PAM <c>Days</c> field.</summary>
    public int DurationDays { get; }

    /// <summary>Gets the value for the PAM <c>Hours</c> field.</summary>
    public int DurationHours { get; }

    /// <summary>Gets the value for the PAM <c>Minutes</c> field.</summary>
    public int DurationMinutes { get; }

    /// <summary>Gets the business reason.</summary>
    public string Reason { get; }

    /// <summary>Gets the total requested duration in minutes.</summary>
    public int TotalMinutes => checked((DurationHours * 60) + DurationMinutes);

    /// <summary>Creates operational form values from persisted hour-based settings.</summary>
    /// <param name="settings">Validated request settings.</param>
    /// <returns>Form values with zero days and zero additional minutes.</returns>
    public static PamRequestFormData FromSettings(RequestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            settings.StartDate,
            settings.StartTime,
            durationDays: 0,
            durationHours: settings.SessionHours,
            durationMinutes: 0,
            settings.RequestReason);
    }

    /// <inheritdoc />
    public override string ToString() => "PAM request form data (redacted)";
}
