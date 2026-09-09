using System.Text.Json;
using System.Text.Json.Serialization;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Infrastructure.Configuration;

/// <summary>
/// Stores non-sensitive application settings as versioned JSON in the user profile.
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    /// <summary>
    /// File name reserved for the non-sensitive application configuration.
    /// </summary>
    public const string SettingsFileName = "app-settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    private readonly string configurationDirectory;
    private readonly string settingsFilePath;

    /// <summary>
    /// Initializes a store under the current user's local application data directory.
    /// </summary>
    public JsonAppSettingsStore()
        : this(GetDefaultConfigurationDirectory())
    {
    }

    /// <summary>
    /// Initializes a store in an explicit directory, primarily for isolated tests.
    /// </summary>
    /// <param name="configurationDirectory">Absolute directory that owns the settings file.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="configurationDirectory"/> is empty or not absolute.
    /// </exception>
    public JsonAppSettingsStore(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory))
        {
            throw new ArgumentException(
                "La carpeta de configuración es obligatoria.",
                nameof(configurationDirectory));
        }

        if (!Path.IsPathFullyQualified(configurationDirectory))
        {
            throw new ArgumentException(
                "La carpeta de configuración debe ser una ruta absoluta.",
                nameof(configurationDirectory));
        }

        this.configurationDirectory = Path.GetFullPath(configurationDirectory);
        settingsFilePath = Path.Combine(this.configurationDirectory, SettingsFileName);
    }

    /// <inheritdoc/>
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = new FileStream(
                settingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var document = await JsonSerializer.DeserializeAsync<AppSettingsDocument>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
            {
                throw new AppSettingsPersistenceException(
                    AppSettingsPersistenceErrorCode.InvalidContent,
                    "El archivo de configuración está vacío.");
            }

            return document.ToDomain();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppSettingsPersistenceException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new AppSettingsPersistenceException(
                AppSettingsPersistenceErrorCode.InvalidContent,
                "El archivo de configuración no contiene JSON válido.",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AppSettingsPersistenceException(
                AppSettingsPersistenceErrorCode.ReadFailed,
                "No se pudo leer la configuración de la aplicación.",
                exception);
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AppSettingsDocument document = AppSettingsDocument.FromDomain(settings);
        byte[] json;

        try
        {
            json = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new AppSettingsPersistenceException(
                AppSettingsPersistenceErrorCode.InvalidContent,
                "La configuración no se pudo convertir a JSON.",
                exception);
        }

        string temporaryFilePath = Path.Combine(
            configurationDirectory,
            $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(configurationDirectory);

            await using (var stream = new FileStream(
                             temporaryFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(settingsFilePath))
            {
                File.Replace(temporaryFilePath, settingsFilePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryFilePath, settingsFilePath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AppSettingsPersistenceException(
                AppSettingsPersistenceErrorCode.WriteFailed,
                "No se pudo guardar la configuración de la aplicación.",
                exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryFilePath);
        }
    }

    private static string GetDefaultConfigurationDirectory()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Windows no proporcionó la carpeta local del usuario.");
        }

        return Path.Combine(localApplicationData, "PamAutomation", "config");
    }

    private static void TryDeleteTemporaryFile(string temporaryFilePath)
    {
        try
        {
            File.Delete(temporaryFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original persistence failure remains the actionable error.
        }
    }
}
