namespace PamAutomation.Core.Outlook;

/// <summary>Identifies an invalid input to an Outlook operation.</summary>
public enum OutlookValidationErrorCode
{
    /// <summary>The configured sending account is absent or invalid.</summary>
    InvalidSmtpAddress,
    /// <summary>The message subject is absent.</summary>
    MissingSubject,
    /// <summary>The message HTML body is absent.</summary>
    MissingHtmlBody,
    /// <summary>A To or Cc recipient is absent or invalid.</summary>
    InvalidRecipient,
    /// <summary>A recipient occurs more than once, including across To and Cc.</summary>
    DuplicateRecipient,
    /// <summary>An expected PAM request identifier is absent from the message body.</summary>
    MissingRequestId,
}
