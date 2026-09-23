using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Infrastructure.Accounts;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PamAutomation.Tests.Infrastructure.Accounts;

[TestClass]
[TestCategory("Unit")]
public sealed class PamAccountServiceTests
{
    [TestMethod]
    public async Task CreatePrincipal_UsesExistingAutomationCredentialAndCannotBeRepeated()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        credentials.For(WindowsPamCredentialStore.CredentialTargetName).Save(Complete("principal"));
        using var service = new PamAccountService(registry, credentials.For);

        PamAccountSummary principal = await service.CreatePrincipalAsync("Omar");
        IReadOnlyList<PamAccountStatus> statuses = await service.GetAccountsAsync();
        PamAccountException exception = await Assert.ThrowsExactlyAsync<PamAccountException>(
            () => service.CreatePrincipalAsync("Otro"));

        Assert.IsTrue(principal.IsPrimary);
        Assert.IsTrue(Assert.ContainsSingle(statuses).HasCredentials);
        Assert.AreEqual(PamAccountErrorCode.PrincipalAlreadyExists, exception.ErrorCode);
    }

    [TestMethod]
    public async Task CreateSecondary_UsesIndependentCredentialTargetAndDoesNotChangePrincipal()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        credentials.For(WindowsPamCredentialStore.CredentialTargetName).Save(Complete("principal"));
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");

        PamAccountSummary secondary = await service.CreateSecondaryAsync("Pepe", Complete("secondary"));

        Assert.IsFalse(secondary.IsPrimary);
        Assert.AreEqual("principal", credentials.For(WindowsPamCredentialStore.CredentialTargetName).LoadForAuthentication()?.Username);
        string secondaryTarget = $"PamAutomation/Accounts/{secondary.Id.ToStorageKey()}/PamCredentials/v1";
        Assert.AreEqual("secondary", credentials.For(secondaryTarget).LoadForAuthentication()?.Username);
    }

    [TestMethod]
    public async Task LoadCredentialsAsync_SelectedIdentity_ReturnsOnlyItsCredential()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        credentials.For(WindowsPamCredentialStore.CredentialTargetName).Save(Complete("principal"));
        using var service = new PamAccountService(registry, credentials.For);
        PamAccountSummary principal = await service.CreatePrincipalAsync("Principal");
        PamAccountSummary secondary = await service.CreateSecondaryAsync("Pepe", Complete("secondary"));

        PamCredentials? principalCredentials = await service.LoadCredentialsAsync(principal.Id);
        PamCredentials? secondaryCredentials = await service.LoadCredentialsAsync(secondary.Id);

        Assert.AreEqual("principal", principalCredentials?.Username);
        Assert.AreEqual("secondary", secondaryCredentials?.Username);
    }

    [TestMethod]
    public async Task LoadCredentialsAsync_UnknownIdentity_IsRejected()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");

        PamAccountException exception = await Assert.ThrowsExactlyAsync<PamAccountException>(
            () => service.LoadCredentialsAsync(PamAccountId.New()));

        Assert.AreEqual(PamAccountErrorCode.AccountNotFound, exception.ErrorCode);
    }

    [TestMethod]
    public async Task UpdateSecondary_EmptySecretsPreserveExistingCredential()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");
        PamAccountSummary secondary = await service.CreateSecondaryAsync("Pepe", Complete("secondary"));

        PamAccountSummary updated = await service.UpdateSecondaryAsync(secondary.Id, "Pepe editado", new PamCredentialUpdate());

        string target = $"PamAutomation/Accounts/{secondary.Id.ToStorageKey()}/PamCredentials/v1";
        Assert.AreEqual("Pepe editado", updated.Name);
        Assert.AreEqual("secondary-password", credentials.For(target).LoadForAuthentication()?.Password);
    }

    [TestMethod]
    public async Task DeleteSecondary_RemovesOnlySecondaryAndPrincipalCannotBeDeleted()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        PamAccountSummary principal = await service.CreatePrincipalAsync("Principal");
        PamAccountSummary secondary = await service.CreateSecondaryAsync("Pepe", Complete("secondary"));

        PamAccountException exception = await Assert.ThrowsExactlyAsync<PamAccountException>(
            () => service.DeleteSecondaryAsync(principal.Id));
        await service.DeleteSecondaryAsync(secondary.Id);

        Assert.AreEqual(PamAccountErrorCode.PrincipalCannotBeDeleted, exception.ErrorCode);
        Assert.HasCount(1, (await registry.LoadAsync()).Accounts);
    }

    [TestMethod]
    public async Task CreateSecondary_WhenRegistryCommitFails_DeletesNewCredential()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");
        registry.FailNextSave = true;

        await Assert.ThrowsExactlyAsync<IOException>(() => service.CreateSecondaryAsync("Pepe", Complete("secondary")));

        Assert.AreEqual(0, credentials.StoreCount);
        Assert.HasCount(1, (await registry.LoadAsync()).Accounts);
    }

    [TestMethod]
    public async Task CreateSecondary_DuplicateNameOrAccountLimit_IsRejected()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");
        await service.CreateSecondaryAsync("Uno", Complete("one"));
        PamAccountException duplicate = await Assert.ThrowsExactlyAsync<PamAccountException>(
            () => service.CreateSecondaryAsync("UNO", Complete("duplicate")));
        for (int index = 2; index < PamAccountRegistryState.MaximumAccounts; index++)
        {
            await service.CreateSecondaryAsync($"Secundario {index}", Complete($"user{index}"));
        }

        PamAccountException limit = await Assert.ThrowsExactlyAsync<PamAccountException>(
            () => service.CreateSecondaryAsync("Otro", Complete("extra")));

        Assert.AreEqual(PamAccountErrorCode.DuplicateName, duplicate.ErrorCode);
        Assert.AreEqual(PamAccountErrorCode.LimitReached, limit.ErrorCode);
        Assert.HasCount(PamAccountRegistryState.MaximumAccounts, (await registry.LoadAsync()).Accounts);
    }

    [TestMethod]
    public async Task UpdateSecondary_WhenRegistryCommitFails_RestoresOriginalCredential()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");
        PamAccountSummary secondary = await service.CreateSecondaryAsync("Pepe", Complete("original"));
        registry.FailNextSave = true;

        await Assert.ThrowsExactlyAsync<IOException>(() => service.UpdateSecondaryAsync(
            secondary.Id, "Nuevo", Complete("replacement")));

        string target = $"PamAutomation/Accounts/{secondary.Id.ToStorageKey()}/PamCredentials/v1";
        Assert.AreEqual("original", credentials.For(target).LoadForAuthentication()?.Username);
        Assert.AreEqual("Pepe", (await registry.LoadAsync()).Accounts[1].Name);
    }

    [TestMethod]
    public async Task DeleteSecondary_WhenRegistryCommitFails_RestoresCredentialAndRegistry()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");
        PamAccountSummary secondary = await service.CreateSecondaryAsync("Pepe", Complete("secondary"));
        registry.FailNextSave = true;

        await Assert.ThrowsExactlyAsync<IOException>(() => service.DeleteSecondaryAsync(secondary.Id));

        string target = $"PamAutomation/Accounts/{secondary.Id.ToStorageKey()}/PamCredentials/v1";
        Assert.IsNotNull(credentials.For(target).LoadForAuthentication());
        Assert.HasCount(2, (await registry.LoadAsync()).Accounts);
    }

    [TestMethod]
    public async Task CreateSecondary_InvalidTotpSeed_IsRejectedBeforePersistence()
    {
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(registry, credentials.For);
        await service.CreatePrincipalAsync("Principal");

        PamAccountException exception = await Assert.ThrowsExactlyAsync<PamAccountException>(() =>
            service.CreateSecondaryAsync("Pepe", new PamCredentialUpdate
            {
                Username = "pepe",
                Password = "password",
                TotpSeed = "INVALID!"
            }));

        Assert.AreEqual(PamAccountErrorCode.InvalidCredentials, exception.ErrorCode);
        Assert.HasCount(1, (await registry.LoadAsync()).Accounts);
        Assert.AreEqual(0, credentials.StoreCount);
    }

    [TestMethod]
    public async Task AccountOperations_RuntimeDiagnostics_DoNotContainCredentialValues()
    {
        const string username = "private-account-user";
        const string password = "private-account-password";
        const string seed = "JBSWY3DPEHPK3PXP";
        using var loggerFactory = new RuntimeDiagnosticLoggerFactory();
        var registry = new MemoryRegistryStore();
        var credentials = new CredentialDirectory();
        using var service = new PamAccountService(
            registry,
            credentials.For,
            loggerFactory.CreateLogger<PamAccountService>());
        await service.CreatePrincipalAsync("Principal");
        PamAccountSummary secondary = await service.CreateSecondaryAsync("Secundario", new PamCredentialUpdate
        {
            Username = username,
            Password = password,
            TotpSeed = seed
        });

        _ = await service.LoadCredentialsAsync(secondary.Id);

        string diagnosticText = string.Join(
            "\n",
            loggerFactory.CaptureSnapshot().Select(logEvent =>
                $"{logEvent.MessageTemplate} {string.Join(' ', logEvent.Properties.Values)}"));
        Assert.DoesNotContain(username, diagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain(password, diagnosticText, StringComparison.Ordinal);
        Assert.DoesNotContain(seed, diagnosticText, StringComparison.Ordinal);
    }

    private static PamCredentialUpdate Complete(string username) => new()
    {
        Username = username,
        Password = $"{username}-password",
        TotpSeed = "JBSWY3DPEHPK3PXP"
    };

    private sealed class MemoryRegistryStore : IPamAccountRegistryStore
    {
        private PamAccountRegistryState state = new();
        public bool FailNextSave { get; set; }
        public Task<PamAccountRegistryState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
        public Task SaveAsync(PamAccountRegistryState state, CancellationToken cancellationToken = default)
        {
            if (FailNextSave) { FailNextSave = false; throw new IOException("Synthetic registry failure."); }
            this.state = state;
            return Task.CompletedTask;
        }
    }

    private sealed class CredentialDirectory
    {
        private readonly Dictionary<string, MemoryCredentialStore> stores = new(StringComparer.Ordinal);
        public int StoreCount => stores.Values.Count(store => store.HasCredentials());
        public IPamCredentialStore For(string target) => stores.TryGetValue(target, out MemoryCredentialStore? store)
            ? store
            : stores[target] = new MemoryCredentialStore();
    }

    private sealed class MemoryCredentialStore : IPamCredentialStore
    {
        private PamCredentials? value;
        public bool HasCredentials() => value is not null;
        public PamCredentials? LoadForAuthentication() => value;
        public void Save(PamCredentialUpdate update)
        {
            string? username = string.IsNullOrWhiteSpace(update.Username) ? value?.Username : update.Username;
            string? password = string.IsNullOrWhiteSpace(update.Password) ? value?.Password : update.Password;
            string? seed = string.IsNullOrWhiteSpace(update.TotpSeed) ? value?.TotpSeed : update.TotpSeed;
            value = new PamCredentials(username!, password!, seed!);
        }
        public void Delete() => value = null;
    }
}
