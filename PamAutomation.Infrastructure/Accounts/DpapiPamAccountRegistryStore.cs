using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;

namespace PamAutomation.Infrastructure.Accounts;

/// <summary>Stores the non-sensitive PAM identity index using DPAPI CurrentUser and atomic replacement.</summary>
public sealed class DpapiPamAccountRegistryStore : IPamAccountRegistryStore
{
    public const string FileName = "pam-accounts.dat";
    private static readonly byte[] Entropy = "PamAutomation/PamAccounts/v1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string directory;
    private readonly string path;

    public DpapiPamAccountRegistryStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PamAutomation", "config"))
    {
    }

    public DpapiPamAccountRegistryStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("La carpeta debe ser absoluta.", nameof(directory));
        this.directory = Path.GetFullPath(directory);
        path = Path.Combine(this.directory, FileName);
    }

    public async Task<PamAccountRegistryState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return new PamAccountRegistryState();
        byte[]? protectedBytes = null;
        byte[]? clearBytes = null;
        try
        {
            protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            RegistryDocument document = JsonSerializer.Deserialize<RegistryDocument>(clearBytes, JsonOptions)
                ?? throw InvalidContent();
            if (document.SchemaVersion != PamAccountRegistryState.CurrentSchemaVersion)
                throw new PamAccountException(PamAccountErrorCode.UnsupportedSchema, "La versión del registro de usuarios PAM no es compatible.");
            if (document.Accounts is null) throw InvalidContent();
            return new PamAccountRegistryState(document.Accounts.Select(item =>
                new PamAccountSummary(new PamAccountId(item.Id), item.Name, item.IsPrimary)).ToArray());
        }
        catch (OperationCanceledException) { throw; }
        catch (PamAccountException) { throw; }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new PamAccountException(PamAccountErrorCode.InvalidContent, "El registro protegido de usuarios PAM está dañado o contiene datos inválidos.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PamAccountException(PamAccountErrorCode.ReadFailed, "No se pudo leer el registro protegido de usuarios PAM.", exception);
        }
        finally
        {
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            if (clearBytes is not null) CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    public async Task SaveAsync(PamAccountRegistryState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[]? clearBytes = null;
        byte[]? protectedBytes = null;
        string temporary = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            var document = new RegistryDocument(
                PamAccountRegistryState.CurrentSchemaVersion,
                state.Accounts.Select(item => new AccountDocument(item.Id.Value, item.Name, item.IsPrimary)).ToArray());
            clearBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(protectedBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        catch (OperationCanceledException) { throw; }
        catch (PamAccountException) { throw; }
        catch (Exception exception) when (exception is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            throw new PamAccountException(PamAccountErrorCode.WriteFailed, "No se pudo guardar el registro protegido de usuarios PAM.", exception);
        }
        finally
        {
            if (clearBytes is not null) CryptographicOperations.ZeroMemory(clearBytes);
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            try { File.Delete(temporary); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static PamAccountException InvalidContent() =>
        new(PamAccountErrorCode.InvalidContent, "El registro protegido de usuarios PAM está incompleto.");

    private sealed record RegistryDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("accounts")] IReadOnlyList<AccountDocument>? Accounts);

    private sealed record AccountDocument(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("isPrimary")] bool IsPrimary);
}
