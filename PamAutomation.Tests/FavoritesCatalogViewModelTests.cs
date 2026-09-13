using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.Tests;

[TestClass]
public sealed class FavoritesCatalogViewModelTests
{
    private static readonly FavoriteCatalogSnapshot Snapshot = new(
        new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero),
        [new FavoriteCatalogEntry("system", "NOSQLBOOSTER", "account", "10.31.32.25")]);

    [TestMethod]
    public async Task InitializeAsync_NoSavedSnapshot_ShowsFirstUseAction()
    {
        var viewModel = new FavoritesCatalogViewModel(new StubCatalogService(null));

        await viewModel.InitializeAsync();

        Assert.IsTrue(viewModel.IsEmpty);
        Assert.AreEqual("Obtener solicitudes favoritas", viewModel.RefreshButtonText);
        Assert.IsEmpty(viewModel.Favorites);
    }

    [TestMethod]
    public async Task InitializeAsync_SavedSnapshot_ShowsFavoritesAndUpdateAction()
    {
        var viewModel = new FavoritesCatalogViewModel(new StubCatalogService(Snapshot));

        await viewModel.InitializeAsync();

        Assert.IsFalse(viewModel.IsEmpty);
        Assert.AreEqual("Actualizar solicitudes favoritas", viewModel.RefreshButtonText);
        Assert.AreEqual("NOSQLBOOSTER - 10.31.32.25", viewModel.Favorites[0].DisplayName);
    }

    [TestMethod]
    public async Task RefreshCommand_FirstSuccessfulRead_ReplacesEmptyStateAndButtonText()
    {
        var service = new StubCatalogService(
            null,
            new FavoriteCatalogRefreshResult(Snapshot, 1, 0));
        var viewModel = new FavoritesCatalogViewModel(service);
        await viewModel.InitializeAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.IsFalse(viewModel.HasError);
        Assert.IsFalse(viewModel.IsEmpty);
        Assert.AreEqual("Actualizar solicitudes favoritas", viewModel.RefreshButtonText);
        Assert.Contains("Se obtuvieron 1", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task RefreshCommand_SafeFailure_PreservesExistingSnapshotAndShowsReason()
    {
        var service = new StubCatalogService(
            Snapshot,
            exception: new FavoriteCatalogException("PAM no está disponible."));
        var viewModel = new FavoritesCatalogViewModel(service);
        await viewModel.InitializeAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual("PAM no está disponible.", viewModel.StatusMessage);
        Assert.HasCount(1, viewModel.Favorites);
    }

    private sealed class StubCatalogService(
        FavoriteCatalogSnapshot? loaded,
        FavoriteCatalogRefreshResult? refreshed = null,
        Exception? exception = null) : IFavoriteCatalogService
    {
        public Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(loaded);

        public Task<FavoriteCatalogRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            exception is not null
                ? Task.FromException<FavoriteCatalogRefreshResult>(exception)
                : Task.FromResult(refreshed ?? throw new InvalidOperationException("No refresh result configured."));
    }
}
