using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Requests;

/// <summary>Captures the exact <c>Favorites</c> count and settings confirmed for one real run.</summary>
public sealed class PamRequestRunAuthorization
{
    private readonly RequestSettings confirmedSettings;
    private int consumed;

    private PamRequestRunAuthorization(
        int expectedFavoriteCount,
        RequestSettings confirmedSettings)
    {
        ExpectedFavoriteCount = expectedFavoriteCount;
        this.confirmedSettings = confirmedSettings;
    }

    /// <summary>Gets the exact number of submissions approved by the user.</summary>
    public int ExpectedFavoriteCount { get; }

    /// <summary>Creates an explicit authorization after the UI confirms the exact run.</summary>
    /// <param name="expectedFavoriteCount">Exact number displayed during confirmation.</param>
    /// <param name="settings">Exact request configuration displayed during confirmation.</param>
    /// <returns>The scoped authorization.</returns>
    public static PamRequestRunAuthorization Confirm(
        int expectedFavoriteCount,
        RequestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (expectedFavoriteCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedFavoriteCount));
        }

        return new(expectedFavoriteCount, settings with { });
    }

    internal bool TryConsume(int observedFavoriteCount, RequestSettings settings) =>
        observedFavoriteCount == ExpectedFavoriteCount &&
        confirmedSettings == settings &&
        Interlocked.CompareExchange(ref consumed, 1, 0) == 0;
}
