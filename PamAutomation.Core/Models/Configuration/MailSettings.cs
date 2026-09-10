using System.Diagnostics;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Models.Configuration;

/// <summary>Contains the private Outlook configuration protected with DPAPI.</summary>
[DebuggerDisplay("Mail settings (redacted)")]
public sealed record MailSettings
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string SmtpAddress { get; init; } = string.Empty;
    public IReadOnlyList<string> ToRecipients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CcRecipients { get; init; } = Array.Empty<string>();
    public string Subject { get; init; } = string.Empty;
    public string HtmlTemplate { get; init; } = string.Empty;
    public IReadOnlyList<OutlookInlineImage> InlineImages { get; init; } = Array.Empty<OutlookInlineImage>();
    public override string ToString() => "Mail settings (redacted)";
}
