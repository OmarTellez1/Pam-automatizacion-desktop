using PamAutomation.App.ViewModels;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Tests;

[TestClass]
public sealed class FavoriteCollectionItemViewModelTests
{
    [TestMethod]
    public void Summary_Collection_ShowsCountAndReason()
    {
        var collection = new FavoriteCollection(
            Guid.NewGuid(),
            "Operaciones",
            "Soporte",
            [new FavoriteAccountKey("system", "application", "account")]);

        var viewModel = new FavoriteCollectionItemViewModel(collection, isActive: true);

        Assert.Contains("1 solicitudes", viewModel.Summary);
        Assert.Contains("Motivo: Soporte", viewModel.Summary);
    }
}
