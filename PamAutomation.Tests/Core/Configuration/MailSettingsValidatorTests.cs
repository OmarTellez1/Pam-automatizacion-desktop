using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests.Core.Configuration;

[TestClass]
public sealed class MailSettingsValidatorTests
{
    [TestMethod]
    public void ValidateAndNormalize_ValidSettings_SanitizesAndCopies()
    {
        var settings = new MailSettings
        {
            SmtpAddress = " sender@example.test ",
            ToRecipients = [" first@example.test "],
            Subject = " Results ",
            HtmlTemplate = "<p onclick=\"bad()\">{{REQUEST_IDS}}</p>"
        };

        MailSettings result = new MailSettingsValidator().ValidateAndNormalize(settings);

        Assert.AreEqual("sender@example.test", result.SmtpAddress);
        Assert.AreEqual("Results", result.Subject);
        Assert.DoesNotContain("onclick", result.HtmlTemplate);
    }

    [TestMethod]
    public void ValidateAndNormalize_MismatchedCid_Rejects()
    {
        var settings = new MailSettings
        {
            SmtpAddress = "sender@example.test",
            ToRecipients = ["first@example.test"],
            Subject = "Results",
            HtmlTemplate = "<p>{{REQUEST_IDS}}</p><img src=\"cid:missing@local\" />"
        };

        OutlookValidationException exception = Assert.ThrowsExactly<OutlookValidationException>(
            () => new MailSettingsValidator().ValidateAndNormalize(settings));

        Assert.AreEqual(OutlookValidationErrorCode.InvalidInlineImage, exception.ErrorCode);
    }
}
