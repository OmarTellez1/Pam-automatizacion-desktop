using System.Diagnostics;

namespace PamAutomation.Core.Outlook;

/// <summary>Represents an Outlook account resolved by its exact SMTP address.</summary>
/// <param name="SmtpAddress">The configured SMTP address.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record OutlookAccount(string SmtpAddress)
{
    private string DebuggerDisplay => "OutlookAccount { Redacted }";

    /// <summary>Returns a redacted representation that does not expose the configured address.</summary>
    public override string ToString() => DebuggerDisplay;
}
