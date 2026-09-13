namespace PamAutomation.Core.Models.Configuration;

/// <summary>
/// Contains the non-sensitive values required to prepare a PAM request.
/// </summary>
public sealed record RequestSettings
{
    /// <summary>
    /// Gets the requested session start date in Ecuador.
    /// </summary>
    public DateOnly StartDate { get; init; }

    /// <summary>
    /// Gets the requested session start time in Ecuador using 24-hour semantics.
    /// </summary>
    public TimeOnly StartTime { get; init; }

    /// <summary>
    /// Gets the requested session duration in whole hours.
    /// </summary>
    public int SessionHours { get; init; } = 1;

    /// <summary>
    /// Gets the business reason supplied for the access request.
    /// </summary>
    public string RequestReason { get; init; } = string.Empty;
}
