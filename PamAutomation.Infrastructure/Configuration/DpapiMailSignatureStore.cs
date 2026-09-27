using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PamAutomation.Core.Interfaces.Configuration;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Infrastructure.Configuration;

/// <summary>Atomically stores the common signature using DPAPI CurrentUser.</summary>
public sealed class DpapiMailSignatureStore : IMailSignatureStore
{
    public const string SettingsFileName = "mail-signature.dat";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PamAutomation/mail-signature/v1");
    private readonly string directory;
    private readonly string path;

    public DpapiMailSignatureStore() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PamAutomation", "config")) { }

    public DpapiMailSignatureStore(string configurationDirectory)
    {
        if (string.IsNullOrWhiteSpace(configurationDirectory) || !Path.IsPathFullyQualified(configurationDirectory))
            throw new ArgumentException("La carpeta de firma debe ser absoluta.", nameof(configurationDirectory));
        directory = Path.GetFullPath(configurationDirectory);
        path = Path.Combine(directory, SettingsFileName);
    }

    public async Task<MailSignature?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        byte[] encrypted = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        byte[]? clear = null;
        try
        {
            clear = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return CollectionMailComposer.ValidateSignature(JsonSerializer.Deserialize<MailSignature>(clear)
                ?? throw new InvalidDataException("La firma protegida está vacía."));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            throw new InvalidDataException("No se pudo leer la firma protegida para este usuario.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
        }
    }

    public async Task SaveAsync(MailSignature signature, CancellationToken cancellationToken = default)
    {
        MailSignature normalized = CollectionMailComposer.ValidateSignature(signature);
        Directory.CreateDirectory(directory);
        byte[] clear = JsonSerializer.SerializeToUtf8Bytes(normalized);
        byte[]? encrypted = null;
        string temporary = Path.Combine(directory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(encrypted, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            try { File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }
}
