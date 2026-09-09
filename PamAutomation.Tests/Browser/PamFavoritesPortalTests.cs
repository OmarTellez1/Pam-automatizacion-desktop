using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("Unit")]
public sealed class PamFavoritesPortalTests
{
    [TestMethod]
    public void Create_ForeignSession_RejectsSession()
    {
        var factory = new PamFavoritesPortalFactory();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(new ForeignSession()));

        Assert.Contains("PamBrowserSessionFactory", exception.Message);
    }

    [TestMethod]
    public async Task ReadAllAsync_BoundPage_ForwardsSnapshot()
    {
        IReadOnlyList<PamFavoriteAccount> accounts =
        [
            new PamFavoriteAccount("system", "application", "account"),
        ];
        var page = new FakeFavoritesPage(accounts);
        IPamFavoritesPortal portal = new PamFavoritesPortal(page);

        IReadOnlyList<PamFavoriteAccount> result = await portal.ReadAllAsync(CancellationToken.None);

        Assert.AreSame(accounts, result);
        Assert.AreEqual(1, page.ReadCalls);
    }

    [TestMethod]
    [DataRow("Page 1/1", 1, 1)]
    [DataRow("page 2/15", 2, 15)]
    [DataRow(" Page 10/10 ", 10, 10)]
    public void Parse_ValidAccessibleLabel_ReturnsPageInformation(
        string label,
        int expectedCurrent,
        int expectedTotal)
    {
        (int current, int total) = PamFavoritesPagination.Parse(label);

        Assert.AreEqual(expectedCurrent, current);
        Assert.AreEqual(expectedTotal, total);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("1/2")]
    [DataRow("Page 0/2")]
    [DataRow("Page 3/2")]
    [DataRow("Page one/two")]
    public void Parse_InvalidAccessibleLabel_ThrowsSafeError(string? label)
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => PamFavoritesPagination.Parse(label));

        Assert.AreEqual("El paginador de Favorites no tiene un formato válido.", exception.Message);
    }

    private sealed class FakeFavoritesPage(IReadOnlyList<PamFavoriteAccount> accounts) : IPamFavoritesPage
    {
        public int ReadCalls { get; private set; }

        public Task<IReadOnlyList<PamFavoriteAccount>> ReadAllFavoritesAsync(CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Task.FromResult(accounts);
        }
    }

    private sealed class ForeignSession : IPamBrowserSession
    {
        public Task<PamNavigationResponse> NavigateAsync(
            Uri uri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
