using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Models.Configuration;

/// <summary>Contains only the recipients and content owned by one collection.</summary>
public sealed record CollectionMailSettings
{
    public IReadOnlyList<string> ToRecipients { get; init; } = [];
    public IReadOnlyList<string> CcRecipients { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string HtmlTemplate { get; init; } = string.Empty;
    public IReadOnlyList<OutlookInlineImage> InlineImages { get; init; } = [];

    /// <summary>Copies legacy/editor content without retaining its sender.</summary>
    public static CollectionMailSettings FromMail(MailSettings mail) => new()
    {
        ToRecipients = Array.AsReadOnly(mail.ToRecipients.ToArray()),
        CcRecipients = Array.AsReadOnly(mail.CcRecipients.ToArray()),
        Subject = mail.Subject,
        HtmlTemplate = mail.HtmlTemplate,
        InlineImages = Array.AsReadOnly(mail.InlineImages.ToArray())
    };

    /// <summary>Creates an independent snapshot of the content.</summary>
    public CollectionMailSettings Copy() => this with
    {
        ToRecipients = Array.AsReadOnly(ToRecipients.ToArray()),
        CcRecipients = Array.AsReadOnly(CcRecipients.ToArray()),
        InlineImages = Array.AsReadOnly(InlineImages.ToArray())
    };

    /// <inheritdoc />
    public override string ToString() => "Collection mail (redacted)";
}
