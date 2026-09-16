using System.Text;
using PamAutomation.Core.Accounts;
using PamAutomation.Infrastructure.Accounts;

namespace PamAutomation.Tests.Infrastructure.Accounts;

[TestClass]
[DoNotParallelize]
public sealed class DpapiPamAccountRegistryStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"PamAutomation-accounts-{Guid.NewGuid():N}");

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsAtomicallyWithoutPlaintextNames()
    {
        var store = new DpapiPamAccountRegistryStore(directory);
        var state = new PamAccountRegistryState(
            [
                new PamAccountSummary(PamAccountId.New(), "Nombre principal único", true),
                new PamAccountSummary(PamAccountId.New(), "Cuenta secundaria única", false)
            ]);

        await store.SaveAsync(state);
        PamAccountRegistryState loaded = await store.LoadAsync();
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(directory, DpapiPamAccountRegistryStore.FileName));

        Assert.HasCount(2, loaded.Accounts);
        Assert.AreEqual("Nombre principal único", loaded.PrimaryAccount?.Name);
        Assert.DoesNotContain("Nombre principal único", Encoding.UTF8.GetString(bytes));
    }

    [TestMethod]
    public async Task Load_MissingFile_ReturnsEmptyStateWithoutCreatingDirectory()
    {
        var store = new DpapiPamAccountRegistryStore(directory);

        PamAccountRegistryState state = await store.LoadAsync();

        Assert.IsEmpty(state.Accounts);
        Assert.IsFalse(Directory.Exists(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
