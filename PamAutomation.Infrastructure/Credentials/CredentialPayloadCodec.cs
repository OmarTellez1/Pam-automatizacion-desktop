using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PamAutomation.Core.Interfaces.Credentials;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Infrastructure.Credentials;

internal static class CredentialPayloadCodec
{
    private const byte CurrentPayloadVersion = 1;
    private const int CredentialBlobLimit = 5 * 512;
    private const int MinimumPayloadLength = 1 + sizeof(int) + sizeof(int);

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static byte[] Encode(PamCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        int passwordLength;
        int totpSeedLength;
        int payloadLength;

        try
        {
            passwordLength = StrictUtf8.GetByteCount(credentials.Password);
            totpSeedLength = StrictUtf8.GetByteCount(credentials.TotpSeed);
            payloadLength = checked(MinimumPayloadLength + passwordLength + totpSeedLength);
        }
        catch (Exception exception) when (exception is EncoderFallbackException or OverflowException)
        {
            throw CreateCorruptedInputException(exception);
        }

        if (payloadLength > CredentialBlobLimit)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.PayloadTooLarge,
                "Las credenciales exceden el tamaño permitido por Windows Credential Manager.");
        }

        byte[] payload = GC.AllocateUninitializedArray<byte>(payloadLength);
        Span<byte> destination = payload;
        destination[0] = CurrentPayloadVersion;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, sizeof(int)), passwordLength);

        int passwordOffset = 1 + sizeof(int);
        int totpLengthOffset = passwordOffset + passwordLength;
        int totpSeedOffset = totpLengthOffset + sizeof(int);

        try
        {
            StrictUtf8.GetBytes(
                credentials.Password.AsSpan(),
                destination.Slice(passwordOffset, passwordLength));
            BinaryPrimitives.WriteInt32LittleEndian(
                destination.Slice(totpLengthOffset, sizeof(int)),
                totpSeedLength);
            StrictUtf8.GetBytes(
                credentials.TotpSeed.AsSpan(),
                destination.Slice(totpSeedOffset, totpSeedLength));
            return payload;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(payload);
            throw;
        }
    }

    public static CredentialSecrets Decode(ReadOnlySpan<byte> payload)
    {
        try
        {
            if (payload.Length < MinimumPayloadLength || payload[0] != CurrentPayloadVersion)
            {
                throw new InvalidDataException();
            }

            int passwordLength = BinaryPrimitives.ReadInt32LittleEndian(
                payload.Slice(1, sizeof(int)));
            int passwordOffset = 1 + sizeof(int);

            if (passwordLength <= 0 || passwordLength > payload.Length - MinimumPayloadLength)
            {
                throw new InvalidDataException();
            }

            int totpLengthOffset = passwordOffset + passwordLength;
            int totpSeedLength = BinaryPrimitives.ReadInt32LittleEndian(
                payload.Slice(totpLengthOffset, sizeof(int)));
            int totpSeedOffset = totpLengthOffset + sizeof(int);

            if (totpSeedLength <= 0 ||
                totpSeedOffset > payload.Length ||
                totpSeedLength != payload.Length - totpSeedOffset)
            {
                throw new InvalidDataException();
            }

            string password = StrictUtf8.GetString(
                payload.Slice(passwordOffset, passwordLength));
            string totpSeed = StrictUtf8.GetString(
                payload.Slice(totpSeedOffset, totpSeedLength));

            return new CredentialSecrets(password, totpSeed);
        }
        catch (Exception exception) when (
            exception is ArgumentOutOfRangeException or
            DecoderFallbackException or
            InvalidDataException)
        {
            throw new CredentialStoreException(
                CredentialStoreErrorCode.CorruptedData,
                "La credencial protegida no tiene un formato válido.",
                exception);
        }
    }

    private static CredentialStoreException CreateCorruptedInputException(Exception innerException)
    {
        return new CredentialStoreException(
            CredentialStoreErrorCode.CorruptedData,
            "Los valores proporcionados no se pueden codificar de forma segura.",
            innerException);
    }
}

internal sealed class CredentialSecrets(string password, string totpSeed)
{
    public string Password { get; } = password;

    public string TotpSeed { get; } = totpSeed;
}
