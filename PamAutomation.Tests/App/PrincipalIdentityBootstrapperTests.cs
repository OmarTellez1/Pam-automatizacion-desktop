using PamAutomation.App.Services;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Tests.App;

[TestClass]
[TestCategory("Unit")]
public sealed class PrincipalIdentityBootstrapperTests
{
    [TestMethod]
    public async Task EnsureAsync_ExistingPrincipal_DoesNotPromptOrWrite()
    {
        var principal = new PamAccountSummary(PamAccountId.New(), "Principal", true);
        var service = new FakePamAccountService([new PamAccountStatus(principal, true)]);
        var bootstrapper = new PrincipalIdentityBootstrapper(service);
        int promptCount = 0;

        PrincipalIdentityBootstrapResult result = await bootstrapper.EnsureAsync(() =>
        {
            promptCount++;
            return "Otro";
        });

        Assert.AreEqual(PrincipalIdentityBootstrapResult.Ready, result);
        Assert.AreEqual(0, promptCount);
        Assert.AreEqual(0, service.CreateCount);
    }

    [TestMethod]
    public async Task EnsureAsync_EmptyRegistry_CreatesOnlyPrincipalMetadata()
    {
        var service = new FakePamAccountService([]);
        var bootstrapper = new PrincipalIdentityBootstrapper(service);

        PrincipalIdentityBootstrapResult result = await bootstrapper.EnsureAsync(() => "  Omar  ");

        Assert.AreEqual(PrincipalIdentityBootstrapResult.Created, result);
        Assert.AreEqual(1, service.CreateCount);
        Assert.AreEqual("  Omar  ", service.LastRequestedName);
    }

    [TestMethod]
    public async Task EnsureAsync_SetupCancelled_DoesNotCreateAnything()
    {
        var service = new FakePamAccountService([]);
        var bootstrapper = new PrincipalIdentityBootstrapper(service);

        PrincipalIdentityBootstrapResult result = await bootstrapper.EnsureAsync(() => null);

        Assert.AreEqual(PrincipalIdentityBootstrapResult.Cancelled, result);
        Assert.AreEqual(0, service.CreateCount);
    }

    [TestMethod]
    public async Task EnsureAsync_ConcurrentPrincipalCreation_RemainsIdempotent()
    {
        var service = new FakePamAccountService([]) { SimulateConcurrentCreation = true };
        var bootstrapper = new PrincipalIdentityBootstrapper(service);

        PrincipalIdentityBootstrapResult result = await bootstrapper.EnsureAsync(() => "Principal");

        Assert.AreEqual(PrincipalIdentityBootstrapResult.Ready, result);
        Assert.AreEqual(1, service.CreateCount);
    }

    private sealed class FakePamAccountService(IReadOnlyList<PamAccountStatus> accounts) : IPamAccountService
    {
        public int CreateCount { get; private set; }
        public string? LastRequestedName { get; private set; }
        public bool SimulateConcurrentCreation { get; init; }

        public Task<IReadOnlyList<PamAccountStatus>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts);

        public Task<PamAccountSummary> CreatePrincipalAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastRequestedName = name;
            if (SimulateConcurrentCreation)
            {
                throw new PamAccountException(
                    PamAccountErrorCode.PrincipalAlreadyExists,
                    "El usuario principal ya fue creado.");
            }

            return Task.FromResult(new PamAccountSummary(PamAccountId.New(), name, true));
        }

        public Task<PamCredentials?> LoadCredentialsAsync(PamAccountId accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> RenameAsync(PamAccountId accountId, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> CreateSecondaryAsync(string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PamAccountSummary> UpdateSecondaryAsync(PamAccountId accountId, string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteSecondaryAsync(PamAccountId accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
