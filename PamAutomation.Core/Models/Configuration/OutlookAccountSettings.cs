namespace PamAutomation.Core.Models.Configuration;

/// <summary>Identifies the Outlook classic account authorized for Pam Automation.</summary>
public sealed record OutlookAccountSettings
{
    /// <summary>Current persisted document schema.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Gets the persisted schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets the exact SMTP address expected in Outlook classic.</summary>
    public string SmtpAddress { get; init; } = string.Empty;
}
