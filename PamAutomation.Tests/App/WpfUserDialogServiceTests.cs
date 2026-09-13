using PamAutomation.App.Services;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Tests.App;

[TestClass]
public sealed class WpfUserDialogServiceTests
{
    [TestMethod]
    public void FormatAutomationStartMessage_EmailAction_ShowsTwelveHourTimeAndRecipients()
    {
        var settings = new AppSettings
        {
            FinalAction = FinalAction.SendEmail,
            Request = new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 12),
                StartTime = new TimeOnly(13, 45),
                SessionHours = 1,
                RequestReason = "Pruebas"
            }
        };
        var mail = new MailSettings
        {
            ToRecipients = ["to@example.test"],
            CcRecipients = ["copy1@example.test", "copy2@example.test"]
        };

        string message = WpfUserDialogService.FormatAutomationStartMessage(settings, mail);

        Assert.Contains("Inicio: 12/09/2026 01:45 PM", message);
        Assert.Contains("Para: to@example.test", message);
        Assert.Contains("CC: copy1@example.test; copy2@example.test", message);
        Assert.Contains("Alcance: Todos los Favorites", message);
    }

    [TestMethod]
    public void FormatAutomationStartMessage_EmailWithoutCc_ShowsNone()
    {
        var settings = new AppSettings
        {
            FinalAction = FinalAction.SendEmail,
            Request = new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 12),
                StartTime = new TimeOnly(0, 5),
                SessionHours = 1,
                RequestReason = "Pruebas"
            }
        };
        var mail = new MailSettings { ToRecipients = ["to@example.test"] };

        string message = WpfUserDialogService.FormatAutomationStartMessage(settings, mail);

        Assert.Contains("Inicio: 12/09/2026 12:05 AM", message);
        Assert.Contains("CC: Ninguno", message);
    }

    [TestMethod]
    public void FormatAutomationStartMessage_CollectionMode_IdentifiesSelectedCollection()
    {
        var settings = new AppSettings
        {
            Request = new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 14),
                StartTime = new TimeOnly(9, 5),
                SessionHours = 1,
                RequestReason = "Acceso de soporte"
            }
        };

        string message = WpfUserDialogService.FormatAutomationStartMessage(
            settings,
            mailSettings: null,
            collectionName: "Accesos con Peter");

        Assert.Contains("Colección: Accesos con Peter", message);
        Assert.DoesNotContain("Alcance: Todos los Favorites", message);
    }
}
