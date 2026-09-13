using System.Diagnostics;

namespace PamAutomation.Core.Outlook;

/// <summary>Contains an already composed message and the request identifiers it must contain.</summary>
/// <param name="SmtpAddress">The exact SMTP address of the Outlook sending account.</param>
/// <param name="Subject">The already composed subject.</param>
/// <param name="HtmlBody">The already composed HTML body.</param>
/// <param name="ToRecipients">The recipients in the To field.</param>
/// <param name="CcRecipients">The recipients in the Cc field.</param>
/// <param name="ExpectedRequestIds">The PAM request identifiers expected in the message body.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record OutlookSendRequest(string SmtpAddress, string Subject, string HtmlBody, IReadOnlyList<string> ToRecipients, IReadOnlyList<string> CcRecipients, IReadOnlyList<string> ExpectedRequestIds)
{
    public IReadOnlyList<OutlookInlineImage> InlineImages { get; init; } = Array.Empty<OutlookInlineImage>();
    private string DebuggerDisplay => "OutlookSendRequest { Redacted }";

    /// <summary>Returns a redacted representation that does not expose private mail settings.</summary>
    public override string ToString() => DebuggerDisplay;
}
