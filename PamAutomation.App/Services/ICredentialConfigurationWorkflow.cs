using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.App.Services;

/// <summary>Executes the explicit, non-destructive checks initiated from credential configuration.</summary>
public interface ICredentialConfigurationWorkflow
{
    /// <summary>Saves the PAM update and authenticates once without creating a request.</summary>
    Task<ConfigurationOperationResult> SaveAndTestPamAsync(
        PamCredentialUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and protects the exact Outlook classic account.</summary>
    Task<ConfigurationOperationResult> SaveAndVerifyOutlookAsync(
        string smtpAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a simple test message from and to the configured Outlook account.</summary>
    Task<ConfigurationOperationResult> SendOutlookTestAsync(
        string smtpAddress,
        CancellationToken cancellationToken = default);
}
