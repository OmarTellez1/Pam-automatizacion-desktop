using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Infrastructure.Configuration;

/// <summary>Stores named favorite collections with DPAPI CurrentUser and atomic replacement.</summary>
public sealed class DpapiFavoriteCollectionStore : IFavoriteCollectionStore
{
    /// <summary>Name of the protected collections document.</summary>
    public const string SettingsFileName = "favorite-collections.dat";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PamAutomation/favorite-collections/v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string configurationDirectory;
    private readonly string settingsFilePath;

    /// <summary>Creates a store in the private configuration directory of the current user.</summary>
    public DpapiFavoriteCollectionStore()
        : this(GetDefaultConfigurationDirectory())
    {
    }

    /// <summary>Creates a store in an explicit absolute directory for isolated tests.</summary>
    public DpapiFavoriteCollectionStore(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory) || !Path.IsPathFullyQualified(configurationDirectory))
        {
            throw new ArgumentException("La carpeta de colecciones debe ser absoluta.", nameof(configurationDirectory));
        }

        this.configurationDirectory = Path.GetFullPath(configurationDirectory);
        settingsFilePath = Path.Combine(this.configurationDirectory, SettingsFileName);
    }

    /// <inheritdoc />
    public async Task<FavoriteCollectionsState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFilePath)) return new FavoriteCollectionsState();

        byte[] protectedBytes = await File.ReadAllBytesAsync(settingsFilePath, cancellationToken).ConfigureAwait(false);
        byte[]? clearBytes = null;
        try
        {
            clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            CollectionDocument document = JsonSerializer.Deserialize<CollectionDocument>(clearBytes, JsonOptions)
                ?? throw new InvalidDataException("El documento protegido de colecciones está vacío.");
            if (document.SchemaVersion is < 1 or > FavoriteCollectionsState.CurrentSchemaVersion ||
                document.Collections is null)
            {
                throw new InvalidDataException("El documento protegido de colecciones no tiene un esquema compatible.");
            }

            FavoriteCollection[] collections = document.Collections
                .Select(item => ToDomain(item, document.SchemaVersion))
                .ToArray();
            if (document.SchemaVersion < 3)
            {
                // The schema transition is the migration marker. Never fall back to global mail in schema 3.
                MailSettings? legacy = await new DpapiMailSettingsStore(configurationDirectory)
                    .LoadAsync(cancellationToken).ConfigureAwait(false);
                collections = collections.Select(collection => new FavoriteCollection(
                    collection.Id, collection.Name, collection.RequestReason, collection.Accounts,
                    collection.IsAllFavorites, legacy is null ? null : CollectionMailSettings.FromMail(legacy))).ToArray();
                await SaveAsync(new FavoriteCollectionsState(collections, document.ActiveCollectionId), cancellationToken)
                    .ConfigureAwait(false);
                // Read back the protected replacement; keep the legacy mail file as a recovery source.
                return await LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            return new FavoriteCollectionsState(collections, document.ActiveCollectionId);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("No se pudieron descifrar las colecciones para este usuario.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El documento protegido de colecciones no contiene JSON válido.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("El documento protegido contiene colecciones inválidas.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (clearBytes is not null) CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        FavoriteCollectionsState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(configurationDirectory);
        var document = new CollectionDocument(
            FavoriteCollectionsState.CurrentSchemaVersion,
            state.ActiveCollectionId,
            state.Collections.Select(ToDocument).ToArray());
        byte[] clearBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        byte[]? protectedBytes = null;
        string temporaryPath = Path.Combine(configurationDirectory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(protectedBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(settingsFilePath)) File.Replace(temporaryPath, settingsFilePath, null);
            else File.Move(temporaryPath, settingsFilePath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static FavoriteCollection ToDomain(CollectionItemDocument document, int schemaVersion)
    {
        if (document.Accounts is null)
        {
            throw new ArgumentException("La colección protegida está incompleta.", nameof(document));
        }

        string? requestReason = schemaVersion == 1
            ? document.Request?.RequestReason
            : document.RequestReason;
        bool isAllFavorites = document.IsAllFavorites ||
            string.Equals(document.Name, FavoriteCollection.AllFavoritesName, StringComparison.OrdinalIgnoreCase);
        return new FavoriteCollection(
            document.Id,
            document.Name,
            requestReason ?? throw new ArgumentException("La colección protegida no contiene un motivo.", nameof(document)),
            document.Accounts.Select(account => new FavoriteAccountKey(
                account.System,
                account.Application,
                account.Account)).ToArray(),
            isAllFavorites,
            document.Mail);
    }

    private static CollectionItemDocument ToDocument(FavoriteCollection collection) => new(
        collection.Id,
        collection.Name,
        collection.RequestReason,
        collection.IsAllFavorites,
        Request: null,
        collection.Accounts.Select(account => new AccountKeyDocument(
            account.SystemName,
            account.ApplicationName,
            account.AccountName)).ToArray(),
        collection.Mail);

    private static string GetDefaultConfigurationDirectory()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Windows no proporcionó la carpeta local del usuario.");
        }

        return Path.Combine(path, "PamAutomation", "config");
    }

    private sealed record CollectionDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("activeCollectionId")] Guid? ActiveCollectionId,
        [property: JsonPropertyName("collections")] IReadOnlyList<CollectionItemDocument>? Collections);

    private sealed record CollectionItemDocument(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("requestReason")] string? RequestReason,
        [property: JsonPropertyName("isAllFavorites")] bool IsAllFavorites,
        [property: JsonPropertyName("request")] LegacyRequestDocument? Request,
        [property: JsonPropertyName("accounts")] IReadOnlyList<AccountKeyDocument>? Accounts,
        [property: JsonPropertyName("mail")] CollectionMailSettings? Mail = null);

    private sealed record LegacyRequestDocument(
        [property: JsonPropertyName("startDate")] DateOnly StartDate,
        [property: JsonPropertyName("startTime")] TimeOnly StartTime,
        [property: JsonPropertyName("sessionHours")] int SessionHours,
        [property: JsonPropertyName("requestReason")] string RequestReason);

    private sealed record AccountKeyDocument(
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("application")] string Application,
        [property: JsonPropertyName("account")] string Account);
}
