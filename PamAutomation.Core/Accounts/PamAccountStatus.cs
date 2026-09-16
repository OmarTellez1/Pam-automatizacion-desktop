namespace PamAutomation.Core.Accounts;

/// <summary>Combines a non-sensitive account identity with credential availability.</summary>
public sealed record PamAccountStatus(PamAccountSummary Account, bool HasCredentials);
