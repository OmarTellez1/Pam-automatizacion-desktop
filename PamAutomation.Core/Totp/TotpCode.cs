using System.Diagnostics;

namespace PamAutomation.Core.Totp;

/// <summary>
/// Contains a generated TOTP code and its validity window without revealing the code in text representations.
/// </summary>
[DebuggerDisplay("TOTP code (redacted)")]
public sealed class TotpCode
{
    internal TotpCode(string value, DateTimeOffset validFrom, DateTimeOffset expiresAt)
    {
        Value = value;
        ValidFrom = validFrom;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Gets the six-digit value intended exclusively for the authentication field.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the inclusive UTC start of the code's validity window.
    /// </summary>
    public DateTimeOffset ValidFrom { get; }

    /// <summary>
    /// Gets the exclusive UTC end of the code's validity window.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the remaining lifetime at a reference time, or zero when the code is outside its validity window.
    /// </summary>
    /// <param name="referenceTime">Trusted time used to evaluate the current window.</param>
    /// <returns>The remaining lifetime, bounded by the code's validity window.</returns>
    public TimeSpan GetRemainingLifetime(DateTimeOffset referenceTime)
    {
        if (referenceTime < ValidFrom || referenceTime >= ExpiresAt)
        {
            return TimeSpan.Zero;
        }

        return ExpiresAt - referenceTime;
    }

    /// <summary>
    /// Determines whether the code is valid and has at least the requested lifetime remaining.
    /// </summary>
    /// <param name="referenceTime">Trusted time used to evaluate the current window.</param>
    /// <param name="minimumLifetime">Minimum lifetime required before submitting the code.</param>
    /// <returns><see langword="true"/> when the code is valid for at least the requested duration.</returns>
    public bool HasMinimumRemainingLifetime(
        DateTimeOffset referenceTime,
        TimeSpan minimumLifetime)
    {
        if (minimumLifetime <= TimeSpan.Zero || minimumLifetime > TotpGenerator.TimeStep)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumLifetime),
                "La vigencia mínima debe ser mayor que cero y no superar el intervalo TOTP.");
        }

        return GetRemainingLifetime(referenceTime) >= minimumLifetime;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "TOTP code (redacted)";
    }
}
