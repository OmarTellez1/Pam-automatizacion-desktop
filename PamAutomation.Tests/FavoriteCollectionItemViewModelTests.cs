using PamAutomation.App.ViewModels;
using PamAutomation.Core.Favorites;

namespace PamAutomation.Tests;

[TestClass]
public sealed class FavoriteCollectionItemViewModelTests
{
    [TestMethod]
    public void AccumulatedCollection_DisablesAllSelectionTogglesAndOnlyItsDeletion()
    {
        var bound = new FavoriteCollection(Guid.NewGuid(), "Bound", "Reason", []);
        var other = new FavoriteCollection(Guid.NewGuid(), "Other", "Reason",
            [new FavoriteAccountKey("system", "app", "account")]);
        var boundRow = new FavoriteCollectionItemViewModel(bound, true, bound.Id);
        var otherRow = new FavoriteCollectionItemViewModel(other, false, bound.Id);
        Assert.IsFalse(boundRow.CanActivate);
        Assert.IsFalse(boundRow.CanDelete);
        Assert.IsFalse(otherRow.CanActivate);
        Assert.IsTrue(otherRow.CanDelete);
        Assert.IsTrue(new FavoriteCollectionItemViewModel(other, false).CanActivate);
    }

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
        Assert.IsTrue(viewModel.CanDelete);
    }

    [TestMethod]
    public void CanDelete_AllFavorites_IsFalse()
    {
        var collection = new FavoriteCollection(
            Guid.NewGuid(),
            FavoriteCollection.AllFavoritesName,
            "Soporte",
            [new FavoriteAccountKey("system", "application", "account")],
            isAllFavorites: true);

        var viewModel = new FavoriteCollectionItemViewModel(collection, isActive: true);

        Assert.IsFalse(viewModel.CanDelete);
    }
}
