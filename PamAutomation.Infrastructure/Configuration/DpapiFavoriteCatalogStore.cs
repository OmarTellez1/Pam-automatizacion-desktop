using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Favorites;

namespace PamAutomation.Infrastructure.Configuration;

/// <summary>Stores the per-user Favorites snapshot as an atomic DPAPI CurrentUser document.</summary>
public sealed class DpapiFavoriteCatalogStore : IFavoriteCatalogStore
{
    /// <summary>Name of the protected Favorites snapshot document.</summary>
    public const string SettingsFileName = "favorite-catalog.dat";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PamAutomation/favorite-catalog/v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string configurationDirectory;
    private readonly string settingsFilePath;

    /// <summary>Creates a store in the current user's private application configuration directory.</summary>
    public DpapiFavoriteCatalogStore()
        : this(GetDefaultConfigurationDirectory())
    {
    }

    /// <summary>Creates a store in an explicit absolute directory, primarily for isolated tests.</summary>
    /// <param name="configurationDirectory">Directory that owns the protected document.</param>
    public DpapiFavoriteCatalogStore(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory) || !Path.IsPathFullyQualified(configurationDirectory))
        {
            throw new ArgumentException("La carpeta del catálogo debe ser absoluta.", nameof(configurationDirectory));
        }

        this.configurationDirectory = Path.GetFullPath(configurationDirectory);
        settingsFilePath = Path.Combine(this.configurationDirectory, SettingsFileName);
    }

    /// <inheritdoc />
    public bool Exists() => File.Exists(settingsFilePath);

    /// <inheritdoc />
    public async Task<FavoriteCatalogSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFilePath)) return null;

        byte[] protectedBytes = await File.ReadAllBytesAsync(settingsFilePath, cancellationToken).ConfigureAwait(false);
        byte[]? clearBytes = null;
        try
        {
            clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            FavoriteCatalogDocument document = JsonSerializer.Deserialize<FavoriteCatalogDocument>(clearBytes, JsonOptions)
                ?? throw new InvalidDataException("El catálogo protegido está vacío.");
            if (document.SchemaVersion != FavoriteCatalogSnapshot.CurrentSchemaVersion ||
                document.Entries is null || document.Entries.Count == 0)
            {
                throw new InvalidDataException("El catálogo protegido no tiene un esquema compatible.");
            }

            FavoriteCatalogEntry[] entries = document.Entries.Select(entry => new FavoriteCatalogEntry(
                entry.System,
                entry.Application,
                entry.Account,
                entry.IpAddress)).ToArray();
            EnsureUnique(entries);
            return new FavoriteCatalogSnapshot(document.RefreshedAtUtc, entries);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("No se pudo descifrar el catálogo de solicitudes favoritas para este usuario.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El catálogo protegido no contiene un documento válido.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("El catálogo protegido contiene solicitudes favoritas inválidas.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (clearBytes is not null) CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        FavoriteCatalogSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureUnique(snapshot.Entries);
        Directory.CreateDirectory(configurationDirectory);
        var document = new FavoriteCatalogDocument(
            FavoriteCatalogSnapshot.CurrentSchemaVersion,
            snapshot.RefreshedAtUtc,
            snapshot.Entries.Select(entry => new FavoriteCatalogEntryDocument(
                entry.SystemName,
                entry.ApplicationName,
                entry.AccountName,
                entry.IpAddress)).ToArray());
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

    private static void EnsureUnique(IReadOnlyList<FavoriteCatalogEntry> entries)
    {
        int uniqueCount = entries
            .Select(entry => (entry.SystemName, entry.ApplicationName, entry.AccountName))
            .Distinct()
            .Count();
        if (entries.Count == 0 || uniqueCount != entries.Count)
        {
            throw new ArgumentException("El catálogo debe contener identidades completas y únicas.", nameof(entries));
        }
    }

    private static string GetDefaultConfigurationDirectory()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Windows no proporcionó la carpeta local del usuario.");
        }

        return Path.Combine(path, "PamAutomation", "config");
    }

    private sealed record FavoriteCatalogDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("refreshedAtUtc")] DateTimeOffset RefreshedAtUtc,
        [property: JsonPropertyName("entries")] IReadOnlyList<FavoriteCatalogEntryDocument>? Entries);

    private sealed record FavoriteCatalogEntryDocument(
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("application")] string Application,
        [property: JsonPropertyName("account")] string Account,
        [property: JsonPropertyName("ipAddress")] string IpAddress);
}
