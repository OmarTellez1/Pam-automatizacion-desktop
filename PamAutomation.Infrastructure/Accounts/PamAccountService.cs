using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Core.Totp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PamAutomation.Infrastructure.Accounts;

/// <summary>Coordinates account metadata and independent Credential Manager entries.</summary>
public sealed class PamAccountService : IPamAccountService, IDisposable
{
    private const string SecondaryTargetPrefix = "PamAutomation/Accounts/";
    private readonly IPamAccountRegistryStore store;
    private readonly Func<string, IPamCredentialStore> credentialStoreFactory;
    private readonly ILogger<PamAccountService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    public PamAccountService(IPamAccountRegistryStore store, ILogger<PamAccountService>? logger = null)
        : this(store, target => new WindowsPamCredentialStore(target), logger)
    {
    }

    public PamAccountService(
        IPamAccountRegistryStore store,
        Func<string, IPamCredentialStore> credentialStoreFactory,
        ILogger<PamAccountService>? logger = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.credentialStoreFactory = credentialStoreFactory ?? throw new ArgumentNullException(nameof(credentialStoreFactory));
        this.logger = logger ?? NullLogger<PamAccountService>.Instance;
    }

    public async Task<IReadOnlyList<PamAccountStatus>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        PamAccountRegistryState state = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return state.Accounts.Select(account => new PamAccountStatus(account, GetCredentialStore(account).HasCredentials())).ToArray();
    }

    public async Task<PamAccountSummary> CreatePrincipalAsync(string name, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeName(name);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PamAccountRegistryState state = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (state.Accounts.Count > 0)
                throw new PamAccountException(PamAccountErrorCode.PrincipalAlreadyExists, "El usuario principal ya fue creado.");
            var principal = new PamAccountSummary(PamAccountId.New(), normalized, true);
            await store.SaveAsync(new PamAccountRegistryState([principal]), cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Principal PAM identity created. AccountId={AccountId}.", principal.Id);
            return principal;
        }
        finally { gate.Release(); }
    }

    public async Task<PamAccountSummary> RenameAsync(PamAccountId accountId, string name, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeName(name);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PamAccountRegistryState state = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            PamAccountSummary current = Find(state, accountId);
            EnsureUniqueName(state, normalized, accountId);
            var renamed = new PamAccountSummary(current.Id, normalized, current.IsPrimary);
            await store.SaveAsync(Replace(state, renamed), cancellationToken).ConfigureAwait(false);
            logger.LogInformation("PAM identity renamed. AccountId={AccountId}; IsPrimary={IsPrimary}.", renamed.Id, renamed.IsPrimary);
            return renamed;
        }
        finally { gate.Release(); }
    }

    public async Task<PamAccountSummary> CreateSecondaryAsync(string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ValidateProvidedTotpSeed(credentials.TotpSeed);
        string normalized = NormalizeName(name);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PamAccountRegistryState state = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (state.PrimaryAccount is null)
                throw new PamAccountException(PamAccountErrorCode.InvalidContent, "Primero debes crear el usuario principal.");
            if (state.Accounts.Count >= PamAccountRegistryState.MaximumAccounts)
                throw new PamAccountException(PamAccountErrorCode.LimitReached, $"Solo puedes registrar hasta {PamAccountRegistryState.MaximumAccounts} usuarios PAM.");
            EnsureUniqueName(state, normalized);
            var account = new PamAccountSummary(PamAccountId.New(), normalized, false);
            IPamCredentialStore credentialsStore = GetCredentialStore(account);
            credentialsStore.Save(credentials);
            try
            {
                await store.SaveAsync(new PamAccountRegistryState([.. state.Accounts, account]), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryDelete(credentialsStore);
                throw;
            }
            logger.LogInformation("Secondary PAM identity created. AccountId={AccountId}.", account.Id);
            return account;
        }
        finally { gate.Release(); }
    }

    public async Task<PamAccountSummary> UpdateSecondaryAsync(PamAccountId accountId, string name, PamCredentialUpdate credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ValidateProvidedTotpSeed(credentials.TotpSeed);
        string normalized = NormalizeName(name);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PamAccountRegistryState state = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            PamAccountSummary current = Find(state, accountId);
            if (current.IsPrimary)
                throw new PamAccountException(PamAccountErrorCode.PrincipalCredentialsManagedSeparately, "Las credenciales del principal se editan desde Editar credenciales.");
            EnsureUniqueName(state, normalized, accountId);
            IPamCredentialStore credentialsStore = GetCredentialStore(current);
            PamCredentials? original = credentialsStore.LoadForAuthentication();
            credentialsStore.Save(credentials);
            var updated = new PamAccountSummary(current.Id, normalized, false);
            try
            {
                await store.SaveAsync(Replace(state, updated), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Restore(credentialsStore, original);
                throw;
            }
            logger.LogInformation("Secondary PAM identity updated. AccountId={AccountId}.", updated.Id);
            return updated;
        }
        finally { gate.Release(); }
    }

    public async Task DeleteSecondaryAsync(PamAccountId accountId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PamAccountRegistryState state = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            PamAccountSummary current = Find(state, accountId);
            if (current.IsPrimary)
                throw new PamAccountException(PamAccountErrorCode.PrincipalCannotBeDeleted, "El usuario principal no se puede eliminar.");
            IPamCredentialStore credentialsStore = GetCredentialStore(current);
            PamCredentials? original = credentialsStore.LoadForAuthentication();
            credentialsStore.Delete();
            try
            {
                await store.SaveAsync(new PamAccountRegistryState(state.Accounts.Where(account => account.Id != accountId).ToArray()), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Restore(credentialsStore, original);
                throw;
            }
            logger.LogInformation("Secondary PAM identity deleted. AccountId={AccountId}.", accountId);
        }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();

    private IPamCredentialStore GetCredentialStore(PamAccountSummary account) =>
        credentialStoreFactory(account.IsPrimary
            ? WindowsPamCredentialStore.CredentialTargetName
            : $"{SecondaryTargetPrefix}{account.Id.ToStorageKey()}/PamCredentials/v1");

    private static PamAccountSummary Find(PamAccountRegistryState state, PamAccountId id) =>
        state.Accounts.FirstOrDefault(account => account.Id == id)
        ?? throw new PamAccountException(PamAccountErrorCode.AccountNotFound, "El usuario PAM seleccionado ya no existe.");

    private static void EnsureUniqueName(PamAccountRegistryState state, string name, PamAccountId? excluded = null)
    {
        if (state.Accounts.Any(account => account.Id != excluded && string.Equals(account.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new PamAccountException(PamAccountErrorCode.DuplicateName, "Ya existe un usuario PAM con ese nombre.");
    }

    private static PamAccountRegistryState Replace(PamAccountRegistryState state, PamAccountSummary replacement) =>
        new(state.Accounts.Select(account => account.Id == replacement.Id ? replacement : account).ToArray());

    private static string NormalizeName(string name)
    {
        try { return PamAccountSummary.NormalizeName(name); }
        catch (ArgumentException exception) { throw new PamAccountException(PamAccountErrorCode.InvalidName, exception.Message, exception); }
    }

    private static void Restore(IPamCredentialStore store, PamCredentials? original)
    {
        if (original is null) { TryDelete(store); return; }
        store.Save(new PamCredentialUpdate { Username = original.Username, Password = original.Password, TotpSeed = original.TotpSeed });
    }

    private static void TryDelete(IPamCredentialStore store)
    {
        try { store.Delete(); } catch { }
    }

    private static void ValidateProvidedTotpSeed(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed)) return;
        try { _ = new TotpGenerator().Generate(seed, DateTimeOffset.UnixEpoch); }
        catch (InvalidBase32SecretException exception)
        {
            throw new PamAccountException(PamAccountErrorCode.InvalidCredentials, exception.Message, exception);
        }
    }
}
