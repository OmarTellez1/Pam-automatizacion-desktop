using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace PamAutomation.Core.Totp;

/// <summary>
/// Generates six-digit RFC 6238 TOTP codes using HMAC-SHA1 and 30-second intervals.
/// </summary>
public sealed class TotpGenerator
{
    private const long TimeStepSeconds = 30;

    /// <summary>
    /// Gets the fixed validity interval required by the PAM authentication flow.
    /// </summary>
    public static TimeSpan TimeStep { get; } = TimeSpan.FromSeconds(TimeStepSeconds);

    /// <summary>
    /// Gets the minimum remaining lifetime required before the authentication flow may submit a code.
    /// </summary>
    public static TimeSpan MinimumSubmissionLifetime { get; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Generates the TOTP code valid at a trusted reference time.
    /// </summary>
    /// <param name="base32Secret">Base32 enrollment seed; whitespace grouping is accepted.</param>
    /// <param name="referenceTime">Trusted time used for the RFC 6238 moving factor.</param>
    /// <returns>A redacted wrapper containing the code and its validity window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="base32Secret"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="referenceTime"/> precedes the Unix epoch.</exception>
    /// <exception cref="InvalidBase32SecretException"><paramref name="base32Secret"/> is not valid Base32 data.</exception>
    public TotpCode Generate(string base32Secret, DateTimeOffset referenceTime)
    {
        ArgumentNullException.ThrowIfNull(base32Secret);

        long unixSeconds = referenceTime.ToUnixTimeSeconds();
        if (unixSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referenceTime),
                "La hora de referencia TOTP no puede ser anterior a la época Unix.");
        }

        byte[] secret = Base32SecretDecoder.Decode(base32Secret);
        byte[] counterBytes = new byte[sizeof(long)];
        byte[]? hash = null;

        try
        {
            long timeCounter = unixSeconds / TimeStepSeconds;
            BinaryPrimitives.WriteInt64BigEndian(counterBytes, timeCounter);
            hash = HMACSHA1.HashData(secret, counterBytes);

            int offset = hash[^1] & 0x0F;
            int binaryCode =
                ((hash[offset] & 0x7F) << 24) |
                ((hash[offset + 1] & 0xFF) << 16) |
                ((hash[offset + 2] & 0xFF) << 8) |
                (hash[offset + 3] & 0xFF);

            string value = (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            long windowStartSeconds = timeCounter * TimeStepSeconds;
            DateTimeOffset validFrom = DateTimeOffset.FromUnixTimeSeconds(windowStartSeconds);

            return new TotpCode(value, validFrom, validFrom.Add(TimeStep));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            CryptographicOperations.ZeroMemory(counterBytes);

            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
    }
}
