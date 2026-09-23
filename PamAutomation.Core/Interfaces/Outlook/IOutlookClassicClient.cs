using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Interfaces.Outlook;

/// <summary>Provides the small Outlook classic operations required by the execution workflow.</summary>
public interface IOutlookClassicClient
{
    /// <summary>Resolves exactly one configured Outlook account by SMTP address.</summary>
    /// <param name="smtpAddress">The exact configured SMTP address.</param>
    /// <param name="cancellationToken">Cancels before or between COM operations.</param>
    /// <returns>The uniquely resolved account.</returns>
    Task<OutlookAccount> ResolveAccountAsync(string smtpAddress, CancellationToken cancellationToken = default);
    /// <summary>Checks an account using a disposable, unsent temporary message.</summary>
    /// <param name="smtpAddress">The exact configured SMTP address.</param>
    /// <param name="cancellationToken">Cancels before or between COM operations.</param>
    Task ValidateAccountAsync(string smtpAddress, CancellationToken cancellationToken = default);
    /// <summary>Checks Sent, Outbox and Drafts for the exact subject and Request ID set.</summary>
    Task<OutlookExistingMessageStatus> FindExistingAsync(
        string smtpAddress,
        string subject,
        IReadOnlyList<string> requestIds,
        CancellationToken cancellationToken = default);
    /// <summary>Observes a submitted message without sending or changing it.</summary>
    /// <param name="smtpAddress">The exact configured SMTP address.</param>
    /// <param name="subject">The exact message subject.</param>
    /// <param name="requestIds">The exact Request ID set.</param>
    /// <param name="includeUnsentFolders">Also checks Outbox and Drafts on the final observation.</param>
    /// <param name="cancellationToken">Cancels before or between COM operations.</param>
    /// <returns>The observed status; failures remain exceptions, never an absent message.</returns>
    Task<OutlookExistingMessageStatus> ObserveDeliveryAsync(
        string smtpAddress,
        string subject,
        IReadOnlyList<string> requestIds,
        bool includeUnsentFolders,
        CancellationToken cancellationToken = default);
    /// <summary>Creates a new message, assigns its account, and asks Outlook to send it.</summary>
    /// <param name="request">A fully composed and validated mail request.</param>
    /// <param name="cancellationToken">Cancels before or between COM operations.</param>
    /// <returns>An in-memory confirmation result.</returns>
    Task<OutlookDeliveryResult> SendAsync(OutlookSendRequest request, CancellationToken cancellationToken = default);
}
