namespace PamAutomation.Core.Resilience;

/// <summary>
/// Centralizes bounded, adaptive timing rules for PAM and Outlook operations.
/// Observation retries are allowed; irreversible actions are never retried by this policy.
/// </summary>
public static class PamOperationTimingPolicy
{
    /// <summary>Maximum elapsed time for one complete Pending browser read.</summary>
    public static TimeSpan PendingReadTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Shared budget for authentication, Pending reads and recovery in one stage.</summary>
    public static TimeSpan PendingRecoveryTimeout { get; } = TimeSpan.FromSeconds(120);

    /// <summary>Maximum wait for an idempotent Pending control action.</summary>
    public static TimeSpan PendingControlTimeout { get; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum time to observe a PAM page becoming ready.</summary>
    public static TimeSpan PortalReadinessTimeout { get; } = TimeSpan.FromSeconds(60);

    /// <summary>Interval between inexpensive readiness observations.</summary>
    public static TimeSpan ObservationInterval { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Maximum time for one transient DOM probe before the outer observation retries it.</summary>
    public static TimeSpan TransientProbeTimeout { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum time to wait for JavaScript-controlled request values to stabilize.</summary>
    public static TimeSpan FormStabilityTimeout { get; } = TimeSpan.FromSeconds(15);

    /// <summary>Maximum time to observe a calendar month change or a selected date.</summary>
    public static TimeSpan CalendarTransitionTimeout { get; } = TimeSpan.FromSeconds(8);

    /// <summary>Overall calendar-selection budget, including opening and navigation.</summary>
    public static TimeSpan CalendarSelectionTimeout { get; } = TimeSpan.FromSeconds(45);

    /// <summary>Maximum complete grid rescans when the portal item count changes mid-read.</summary>
    public const int GridScanMaximumAttempts = 5;

    /// <summary>Delay before rescanning a grid whose total changed during the prior scan.</summary>
    public static TimeSpan GridScanRetryDelay { get; } = TimeSpan.FromMilliseconds(750);

    /// <summary>Maximum number of complete Pending refreshes used for eventual consistency.</summary>
    public const int PendingComparisonMaximumAttempts = 10;

    /// <summary>Delay between complete Pending refreshes.</summary>
    public static TimeSpan PendingComparisonRetryDelay { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum read attempts before replacing a stalled Pending browser session.</summary>
    public const int PendingReadMaximumAttemptsPerSession = 2;

    /// <summary>Maximum authenticated read sessions used after submissions; requests are never resubmitted.</summary>
    public const int PendingReadSessionMaximumAttempts = 2;

    /// <summary>Delay before reloading Pending or rebuilding its temporary read session.</summary>
    public static TimeSpan PendingReadRecoveryDelay { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum observations after Outlook accepts a send request.</summary>
    public const int OutlookConfirmationMaximumAttempts = 15;

    /// <summary>Delay between read-only Outlook delivery observations.</summary>
    public static TimeSpan OutlookConfirmationPollInterval { get; } = TimeSpan.FromSeconds(4);

    /// <summary>Maximum attempts to reconnect to Outlook COM after launching Outlook.</summary>
    public const int OutlookStartupMaximumAttempts = 15;

    /// <summary>Delay between Outlook COM startup observations.</summary>
    public static TimeSpan OutlookStartupRetryDelay { get; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum time to locate a newly opened normal Edge window.</summary>
    public static TimeSpan InteractiveBrowserStartTimeout { get; } = TimeSpan.FromSeconds(60);

    /// <summary>Maximum manual-intervention window for the corporate certificate page.</summary>
    public static TimeSpan InteractiveLoginControlTimeout { get; } = TimeSpan.FromMinutes(3);

    /// <summary>Maximum time to observe the result of credentials or TOTP in normal Edge.</summary>
    public static TimeSpan InteractivePortalResponseTimeout { get; } = TimeSpan.FromSeconds(60);
}
