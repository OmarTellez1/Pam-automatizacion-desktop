namespace PamAutomation.App.Services;

/// <summary>Describes the non-sensitive configuration state shown by the credentials window.</summary>
public sealed record CredentialConfigurationState(
    bool HasPamCredentials,
    bool HasOutlookAccount,
    string OutlookSmtpAddress);
