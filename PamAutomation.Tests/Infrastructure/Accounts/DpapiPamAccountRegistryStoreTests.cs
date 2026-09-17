using System.Text;
using System.Security.Cryptography;
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

    [TestMethod]
    public async Task Load_TamperedProtectedFile_IsRejectedWithoutDeletingEvidence()
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, DpapiPamAccountRegistryStore.FileName);
        await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03, 0x04]);
        var store = new DpapiPamAccountRegistryStore(directory);

        PamAccountException exception = await Assert.ThrowsExactlyAsync<PamAccountException>(() => store.LoadAsync());

        Assert.AreEqual(PamAccountErrorCode.InvalidContent, exception.ErrorCode);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task Load_UnsupportedSchema_IsRejectedWithoutReinterpretingContent()
    {
        Directory.CreateDirectory(directory);
        const string json = "{\"schemaVersion\":99,\"accounts\":[]}";
        byte[] clearBytes = Encoding.UTF8.GetBytes(json);
        byte[] entropy = "PamAutomation/PamAccounts/v1"u8.ToArray();
        byte[] protectedBytes = ProtectedData.Protect(clearBytes, entropy, DataProtectionScope.CurrentUser);
        try
        {
            await File.WriteAllBytesAsync(
                Path.Combine(directory, DpapiPamAccountRegistryStore.FileName),
                protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
        var store = new DpapiPamAccountRegistryStore(directory);

        PamAccountException exception = await Assert.ThrowsExactlyAsync<PamAccountException>(() => store.LoadAsync());

        Assert.AreEqual(PamAccountErrorCode.UnsupportedSchema, exception.ErrorCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
