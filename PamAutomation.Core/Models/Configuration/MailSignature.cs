using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Models.Configuration;

/// <summary>The optional signature shared by the principal user's collections.</summary>
public sealed record MailSignature
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string HtmlContent { get; init; } = string.Empty;
    public IReadOnlyList<OutlookInlineImage> InlineImages { get; init; } = [];
    public override string ToString() => "Mail signature (redacted)";
}
