using PamAutomation.Core.Execution;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Interfaces.Execution;

/// <summary>Coordinates complete PAM runs without persisting their outcomes.</summary>
public interface IPamAutomationExecutionService
{
    Task<PamExecutionPreparation> PrepareRealAsync(
        PamCredentials credentials,
        AppSettings settings,
        DateTimeOffset ecuadorNow,
        CancellationToken cancellationToken = default);

    Task<PamExecutionResult> ExecuteRealAsync(
        PamCredentials credentials,
        PamExecutionPreparation preparation,
        DateTimeOffset ecuadorNow,
        MailSettings? mailSettings = null,
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
