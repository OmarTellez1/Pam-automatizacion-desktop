using PamAutomation.App.ViewModels;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Tests;

[TestClass]
public sealed class FavoriteCollectionItemViewModelTests
{
    [TestMethod]
    public void Summary_AfternoonStart_UsesTwelveHourFormat()
    {
        var collection = new FavoriteCollection(
            Guid.NewGuid(),
            "Operaciones",
            new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 14),
                StartTime = new TimeOnly(17, 5),
                SessionHours = 2,
                RequestReason = "Soporte"
            },
            [new FavoriteAccountKey("system", "application", "account")]);

        var viewModel = new FavoriteCollectionItemViewModel(collection, isActive: true);

        Assert.Contains("05:05 PM", viewModel.Summary);
    }
}
