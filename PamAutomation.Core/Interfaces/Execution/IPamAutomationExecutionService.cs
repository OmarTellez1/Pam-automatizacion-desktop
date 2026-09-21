using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Requests;

namespace PamAutomation.Core.Interfaces.Execution;

/// <summary>Coordinates complete PAM runs without persisting their outcomes.</summary>
public interface IPamAutomationExecutionService
{
    Task<PamExecutionPreparation> PrepareRealAsync(
        PamCredentials credentials,
        AppSettings settings,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default);

    /// <summary>Prepares a real run restricted to exact identities from one active collection.</summary>
    /// <param name="credentials">Protected PAM credentials.</param>
    /// <param name="settings">Application settings containing the collection-specific request.</param>
    /// <param name="selectedAccounts">Exact Favorites identities selected by the user.</param>
    /// <param name="ecuadorNow">Current Ecuador time.</param>
    /// <param name="cancellationToken">Cancels the read-only preparation.</param>
    /// <returns>An immutable preparation safe to confirm and execute.</returns>
    Task<PamExecutionPreparation> PrepareCollectionRealAsync(
        PamCredentials credentials,
        AppSettings settings,
        IReadOnlyList<FavoriteAccountKey> selectedAccounts,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default);

    Task<PamExecutionResult> ExecuteRealAsync(
        PamCredentials credentials,
        PamExecutionPreparation preparation,
        DateTimeOffset ecuadorNow,
        MailSettings? mailSettings = null,
        CancellationToken cancellationToken = default,
        IProgress<PamRequestSubmissionProgress>? progress = null);

    /// <summary>Sends the result email with a complete set of existing Pending Request IDs.</summary>
    Task<PamExecutionResult> CompleteWithExistingPendingAsync(
        PamExecutionPreparation preparation,
        MailSettings mailSettings,
        CancellationToken cancellationToken = default);

    Task<PamExecutionResult> ExecuteTestAsync(
        PamCredentials credentials,
        AppSettings settings,
        DateTimeOffset ecuadorNow,
        MailSettings? mailSettings = null,
        CancellationToken cancellationToken = default);

    Task<OutlookDeliveryResult> SendResultEmailAsync(
        MailSettings mailSettings,
        IReadOnlyList<string> requestIds,
        bool isTest,
        CancellationToken cancellationToken = default);
}
