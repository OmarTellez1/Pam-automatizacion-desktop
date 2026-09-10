using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Infrastructure.Configuration;

/// <summary>Stores mail settings as an atomic DPAPI CurrentUser encrypted document.</summary>
public sealed class DpapiMailSettingsStore : IMailSettingsStore
{
    public const string SettingsFileName = "mail-settings.dat";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PamAutomation/mail-settings/v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string configurationDirectory;
    private readonly string settingsFilePath;

    public DpapiMailSettingsStore() : this(GetDefaultConfigurationDirectory()) { }

    public DpapiMailSettingsStore(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory) || !Path.IsPathFullyQualified(configurationDirectory))
        {
            throw new ArgumentException("La carpeta de configuración de correo debe ser absoluta.", nameof(configurationDirectory));
        }

        this.configurationDirectory = Path.GetFullPath(configurationDirectory);
        settingsFilePath = Path.Combine(this.configurationDirectory, SettingsFileName);
    }

    public bool Exists() => File.Exists(settingsFilePath);

    public async Task<MailSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFilePath)) return null;
        byte[] protectedBytes = await File.ReadAllBytesAsync(settingsFilePath, cancellationToken).ConfigureAwait(false);
        byte[]? plainBytes = null;
        try
        {
            plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<MailSettings>(plainBytes, JsonOptions)
                ?? throw new InvalidDataException("La configuración protegida de correo está vacía.");
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("No se pudo descifrar la configuración de correo para este usuario.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (plainBytes is not null) CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public async Task SaveAsync(MailSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(configurationDirectory);
        byte[] plainBytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        byte[]? protectedBytes = null;
        string temporaryPath = Path.Combine(configurationDirectory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
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
            CryptographicOperations.ZeroMemory(plainBytes);
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            try { File.Delete(temporaryPath); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string GetDefaultConfigurationDirectory()
    {
        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Windows no proporcionó la carpeta local del usuario.");
        return Path.Combine(path, "PamAutomation", "config");
    }
}
