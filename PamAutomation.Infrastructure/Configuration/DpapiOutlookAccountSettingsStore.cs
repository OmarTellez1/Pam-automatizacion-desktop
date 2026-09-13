using System.Security.Cryptography;
using System.Net.Mail;
using System.Text.Json;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Infrastructure.Configuration;

/// <summary>Stores the Outlook account as a DPAPI CurrentUser protected document.</summary>
public sealed class DpapiOutlookAccountSettingsStore : IOutlookAccountSettingsStore
{
    /// <summary>Name of the protected account document.</summary>
    public const string SettingsFileName = "outlook-account.dat";

    private static readonly byte[] Entropy = "PamAutomation.OutlookAccount.v1"u8.ToArray();
    private readonly string directory;
    private readonly string path;

    /// <summary>Creates a store under the normal private application directory.</summary>
    public DpapiOutlookAccountSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PamAutomation",
            "config"))
    {
    }

    /// <summary>Creates a store under a supplied directory, primarily for isolated tests.</summary>
    /// <param name="directory">Private directory that owns the protected document.</param>
    public DpapiOutlookAccountSettingsStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = Path.GetFullPath(directory);
        path = Path.Combine(this.directory, SettingsFileName);
    }

    /// <inheritdoc />
    public bool Exists() => File.Exists(path);

    /// <inheritdoc />
    public async Task<OutlookAccountSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;

        byte[] protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        byte[] clearBytes;
        try
        {
            clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("La configuración protegida de Outlook no se pudo descifrar.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }

        try
        {
            OutlookAccountSettings? settings = JsonSerializer.Deserialize<OutlookAccountSettings>(clearBytes);
            if (settings is null || settings.SchemaVersion != OutlookAccountSettings.CurrentSchemaVersion)
            {
                throw new InvalidDataException("La configuración protegida de Outlook no tiene un esquema compatible.");
            }

            string? normalized = NormalizeSmtp(settings.SmtpAddress);
            if (normalized is null)
            {
                throw new InvalidDataException("La configuración protegida de Outlook contiene una cuenta inválida.");
            }

            return settings with { SmtpAddress = normalized };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("La configuración protegida de Outlook no contiene un documento válido.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(OutlookAccountSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != OutlookAccountSettings.CurrentSchemaVersion)
        {
            throw new ArgumentException("La configuración de Outlook no tiene un esquema compatible.", nameof(settings));
        }

        string? normalized = NormalizeSmtp(settings.SmtpAddress);
        if (normalized is null)
        {
            throw new ArgumentException("La cuenta de Outlook no es una dirección SMTP válida.", nameof(settings));
        }

        Directory.CreateDirectory(directory);
        byte[] clearBytes = JsonSerializer.SerializeToUtf8Bytes(settings with { SmtpAddress = normalized });
        byte[] protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        string temporaryPath = Path.Combine(directory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
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
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string? NormalizeSmtp(string value)
    {
        string normalized = value.Trim();
        return MailAddress.TryCreate(normalized, out MailAddress? address) &&
               string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)
            ? address.Address
            : null;
    }
}
