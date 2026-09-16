using System.ComponentModel;
using System.Security.Cryptography;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Infrastructure.Credentials;

/// <summary>
/// Stores one versioned PAM credential in the current user's Windows Credential Manager.
/// </summary>
public sealed class WindowsPamCredentialStore : IPamCredentialStore
{
    /// <summary>
    /// Stable target name used for the PAM credential entry.
    /// </summary>
    public const string CredentialTargetName = "PamAutomation/PamCredentials/v1";

    private readonly IWindowsCredentialApi credentialApi;
    private readonly string credentialTargetName;

    /// <summary>
    /// Initializes a store backed by the Windows Credential Management API.
    /// </summary>
    public WindowsPamCredentialStore()
        : this(new WindowsCredentialApi(), CredentialTargetName)
    {
    }

    /// <summary>Initializes a store for an explicit application-owned Credential Manager target.</summary>
    /// <param name="credentialTargetName">Stable target reserved for one PAM identity.</param>
    public WindowsPamCredentialStore(string credentialTargetName)
        : this(new WindowsCredentialApi(), credentialTargetName)
    {
    }

    internal WindowsPamCredentialStore(IWindowsCredentialApi credentialApi)
        : this(credentialApi, CredentialTargetName)
    {
    }

    internal WindowsPamCredentialStore(IWindowsCredentialApi credentialApi, string credentialTargetName)
    {
        this.credentialApi = credentialApi ??
            throw new ArgumentNullException(nameof(credentialApi));
        if (string.IsNullOrWhiteSpace(credentialTargetName))
            throw new ArgumentException("El destino de credenciales es obligatorio.", nameof(credentialTargetName));
        this.credentialTargetName = credentialTargetName;
    }

    /// <inheritdoc/>
    public bool HasCredentials()
    {
        return LoadForAuthentication() is not null;
    }

    /// <inheritdoc/>
    public PamCredentials? LoadForAuthentication()
    {
        CredentialManagerEntry? entry = ReadEntry();
        if (entry is null)
        {
            return null;
        }

        try
        {
            CredentialSecrets secrets = CredentialPayloadCodec.Decode(entry.CredentialBlob);
            return new PamCredentials(entry.Username, secrets.Password, secrets.TotpSeed);
        }
        catch (CredentialStoreException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw CreateCorruptedDataException(exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entry.CredentialBlob);
        }
    }

    /// <inheritdoc/>
    public void Save(PamCredentialUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        PamCredentials? existing = LoadForAuthentication();
        string? username = SelectUpdatedValue(update.Username, existing?.Username);
        string? password = SelectUpdatedValue(update.Password, existing?.Password);
        string? totpSeed = SelectUpdatedValue(update.TotpSeed, existing?.TotpSeed);

        if (username is null || password is null || totpSeed is null)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.IncompleteUpdate,
                "La primera configuración requiere usuario, contraseña y semilla TOTP.");
        }

        if (username.Length > CredentialNativeMethods.MaximumUsernameLength)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.PayloadTooLarge,
                "Las credenciales exceden el tamaño permitido por Windows Credential Manager.");
        }

        var credentials = new PamCredentials(username, password, totpSeed);
        byte[] credentialBlob = CredentialPayloadCodec.Encode(credentials);

        try
        {
            credentialApi.Write(new CredentialManagerEntry(
                credentialTargetName,
                credentials.Username,
                credentialBlob));
        }
        catch (Win32Exception exception)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.WriteFailed,
                "Windows Credential Manager no pudo guardar las credenciales PAM.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialBlob);
        }
    }

    /// <inheritdoc/>
    public void Delete()
    {
        try
        {
            credentialApi.Delete(credentialTargetName);
        }
        catch (Win32Exception exception)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.DeleteFailed,
                "Windows Credential Manager no pudo eliminar las credenciales PAM.",
                exception);
        }
    }

    private CredentialManagerEntry? ReadEntry()
    {
        try
        {
            return credentialApi.Read(credentialTargetName);
        }
        catch (InvalidDataException exception)
        {
            throw CreateCorruptedDataException(exception);
        }
        catch (Win32Exception exception)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.ReadFailed,
                "Windows Credential Manager no pudo leer las credenciales PAM.",
                exception);
        }
    }

    private static string? SelectUpdatedValue(string? proposedValue, string? existingValue)
    {
        return string.IsNullOrWhiteSpace(proposedValue) ? existingValue : proposedValue;
    }

    private static CredentialStoreException CreateCorruptedDataException(Exception innerException)
    {
        return new CredentialStoreException(
            CredentialStoreErrorCode.CorruptedData,
            "La credencial PAM protegida está dañada o incompleta.",
            innerException);
    }
}
