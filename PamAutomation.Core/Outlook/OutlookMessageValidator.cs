using System.Net.Mail;

namespace PamAutomation.Core.Outlook;

/// <summary>Validates mail inputs before a COM operation can create or send a message.</summary>
public static class OutlookMessageValidator
{
    /// <summary>Validates an SMTP account address.</summary>
    /// <param name="smtpAddress">The configured SMTP address.</param>
    public static void ValidateSmtpAddress(string? smtpAddress)
    {
        if (!IsValidAddress(smtpAddress))
        {
            throw new OutlookValidationException(OutlookValidationErrorCode.InvalidSmtpAddress);
        }
    }

    /// <summary>Validates all fields required to send a composed message.</summary>
    /// <param name="request">The composed message request.</param>
    public static void ValidateForSend(OutlookSendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSmtpAddress(request.SmtpAddress);
        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new OutlookValidationException(OutlookValidationErrorCode.MissingSubject);
        }

        if (string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            throw new OutlookValidationException(OutlookValidationErrorCode.MissingHtmlBody);
        }

        HashSet<string> recipients = new(StringComparer.OrdinalIgnoreCase);
        if (request.ToRecipients is null || request.ToRecipients.Count == 0)
        {
            throw new OutlookValidationException(OutlookValidationErrorCode.InvalidRecipient);
        }

        ValidateRecipients(request.ToRecipients, recipients);
        ValidateRecipients(request.CcRecipients, recipients);
        try
        {
            OutlookHtmlComposer.ValidateComposedBody(request.HtmlBody, request.ExpectedRequestIds);
        }
        catch (OutlookHtmlCompositionException exception)
        {
            OutlookValidationErrorCode errorCode = exception.ErrorCode is
                OutlookHtmlCompositionErrorCode.MissingRequestIds or
                OutlookHtmlCompositionErrorCode.InvalidRequestId or
                OutlookHtmlCompositionErrorCode.DuplicateRequestId or
                OutlookHtmlCompositionErrorCode.MissingComposedRequestId
                    ? OutlookValidationErrorCode.MissingRequestId
                    : OutlookValidationErrorCode.UnsafeHtmlBody;
            throw new OutlookValidationException(errorCode, exception);
        }
    }

    private static void ValidateRecipients(IReadOnlyList<string>? values, HashSet<string> recipients)
    {
        foreach (string? value in values ?? [])
        {
            if (!IsValidAddress(value))
            {
                throw new OutlookValidationException(OutlookValidationErrorCode.InvalidRecipient);
            }

            if (!recipients.Add(value))
            {
                throw new OutlookValidationException(OutlookValidationErrorCode.DuplicateRecipient);
            }
        }
    }

    private static bool IsValidAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            return false;
        }

        try
        {
            MailAddress address = new(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
