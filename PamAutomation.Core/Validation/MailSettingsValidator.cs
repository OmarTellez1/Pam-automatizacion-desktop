using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Validation;

public sealed class MailSettingsValidator
{
    public MailSettings ValidateAndNormalize(MailSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != MailSettings.CurrentSchemaVersion)
        {
            throw new OutlookValidationException(OutlookValidationErrorCode.InvalidMailSettingsVersion);
        }

        string sanitizedTemplate = OutlookHtmlComposer.SanitizeTemplate(settings.HtmlTemplate);
        string[] to = CopyRecipients(settings.ToRecipients);
        string[] cc = CopyRecipients(settings.CcRecipients);
        string composed = OutlookHtmlComposer.Compose(sanitizedTemplate, ["1"]);
        OutlookMessageValidator.ValidateForSend(new OutlookSendRequest(
            settings.SmtpAddress.Trim(), settings.Subject.Trim(), composed, to, cc, ["1"])
        {
            InlineImages = settings.InlineImages
        });
        return settings with
        {
            SmtpAddress = settings.SmtpAddress.Trim(),
            Subject = settings.Subject.Trim(),
            HtmlTemplate = sanitizedTemplate,
            ToRecipients = to,
            CcRecipients = cc
        };
    }

    private static string[] CopyRecipients(IReadOnlyList<string>? recipients) =>
        (recipients ?? []).Select(static value => value?.Trim() ?? string.Empty).ToArray();
}
