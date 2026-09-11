namespace PamAutomation.Core.Diagnostics;

/// <summary>Contains the allowlisted application state included in a diagnostic export.</summary>
/// <param name="CredentialsConfigured">Whether PAM credentials are configured.</param>
/// <param name="RequestConfigured">Whether request settings are configured.</param>
/// <param name="MailConfigured">Whether mail settings are configured.</param>
/// <param name="LastExecution">Optional safe summary of the last completed execution.</param>
public sealed record DiagnosticExportContext(
    bool CredentialsConfigured,
    bool RequestConfigured,
    bool MailConfigured,
    DiagnosticExecutionSummary? LastExecution);

/// <summary>Contains execution counts and statuses without Request ID or portal identities.</summary>
/// <param name="Mode">Test or real execution mode.</param>
/// <param name="CompletedAt">Completion timestamp reported by the execution.</param>
/// <param name="FinalAction">Final action selected for the run.</param>
/// <param name="FavoriteCount">Number of Favorites rows discovered.</param>
/// <param name="ProcessedCount">Number of rows that reached a known result.</param>
/// <param name="ConflictCount">Number of detected conflicts.</param>
/// <param name="RequestIdCount">Count of Request IDs without their values.</param>
/// <param name="MailStatus">Optional Outlook delivery status name.</param>
public sealed record DiagnosticExecutionSummary(
    string Mode,
    DateTimeOffset CompletedAt,
    string FinalAction,
    int FavoriteCount,
    int ProcessedCount,
    int ConflictCount,
    int RequestIdCount,
    string? MailStatus);
