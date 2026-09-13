using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Core.Favorites;

/// <summary>Coordinates protected local Favorites snapshots with complete live portal reads.</summary>
public sealed class FavoriteCatalogService : IFavoriteCatalogService
{
    private readonly IPamFavoritesReader favoritesReader;
    private readonly IPamCredentialStore credentialStore;
    private readonly IFavoriteCatalogStore catalogStore;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes the catalog synchronization workflow.</summary>
    public FavoriteCatalogService(
        IPamFavoritesReader favoritesReader,
        IPamCredentialStore credentialStore,
        IFavoriteCatalogStore catalogStore,
        TimeProvider timeProvider)
    {
        this.favoritesReader = favoritesReader ?? throw new ArgumentNullException(nameof(favoritesReader));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default) =>
        catalogStore.LoadAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<FavoriteCatalogRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PamCredentials credentials = credentialStore.LoadForAuthentication()
            ?? throw new FavoriteCatalogException(
                "Primero configura y verifica tus credenciales PAM para obtener las solicitudes favoritas.");

        FavoriteCatalogSnapshot? previous = await catalogStore
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<PamFavoriteAccount> favorites;
        try
        {
            favorites = await favoritesReader
                .ReadAllAsync(credentials, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FavoriteCatalogException(
                "No fue posible obtener las solicitudes favoritas desde PAM. Comprueba la conexión e inténtalo nuevamente.",
                exception);
        }

        FavoriteCatalogEntry[] entries;
        try
        {
            entries = favorites.Select(account => new FavoriteCatalogEntry(
                account.SystemName,
                account.ApplicationName,
                account.AccountName,
                account.IpAddress ?? string.Empty)).ToArray();
            EnsureUnique(entries);
        }
        catch (ArgumentException exception)
        {
            throw new FavoriteCatalogException(
                "PAM devolvió una solicitud favorita sin Application, Account, System o IP Address completos.",
                exception);
        }

        var snapshot = new FavoriteCatalogSnapshot(timeProvider.GetUtcNow(), entries);
        HashSet<(string System, string Application, string Account)> currentKeys = CreateKeySet(entries);
        HashSet<(string System, string Application, string Account)> previousKeys = previous is null
            ? []
            : CreateKeySet(previous.Entries);
        int addedCount = currentKeys.Count(key => !previousKeys.Contains(key));
        int removedCount = previousKeys.Count(key => !currentKeys.Contains(key));

        await catalogStore.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return new FavoriteCatalogRefreshResult(snapshot, addedCount, removedCount);
    }

    private static HashSet<(string System, string Application, string Account)> CreateKeySet(
        IEnumerable<FavoriteCatalogEntry> entries) =>
        entries.Select(entry => (entry.SystemName, entry.ApplicationName, entry.AccountName)).ToHashSet();

    private static void EnsureUnique(IReadOnlyList<FavoriteCatalogEntry> entries)
    {
        HashSet<(string System, string Application, string Account)> keys = CreateKeySet(entries);
        if (keys.Count != entries.Count)
        {
            throw new ArgumentException("Favorites devolvió identidades repetidas.", nameof(entries));
        }
    }
}
