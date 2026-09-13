using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Requests;

/// <summary>Captures the exact <c>Favorites</c> count and settings confirmed for one real run.</summary>
public sealed class PamRequestRunAuthorization
{
    private readonly RequestSettings confirmedSettings;
    private readonly IReadOnlyList<FavoriteAccountKey> selectedAccounts;
    private int consumed;

    private PamRequestRunAuthorization(
        int expectedFavoriteCount,
        RequestSettings confirmedSettings,
        IReadOnlyList<FavoriteAccountKey>? selectedAccounts)
    {
        ExpectedFavoriteCount = expectedFavoriteCount;
        this.confirmedSettings = confirmedSettings;
        this.selectedAccounts = selectedAccounts?.ToArray() ?? [];
    }

    /// <summary>Gets the exact number of submissions approved by the user.</summary>
    public int ExpectedFavoriteCount { get; }

    /// <summary>Creates an explicit authorization after the UI confirms the exact run.</summary>
    /// <param name="expectedFavoriteCount">Exact number displayed during confirmation.</param>
    /// <param name="settings">Exact request configuration displayed during confirmation.</param>
    /// <returns>The scoped authorization.</returns>
    public static PamRequestRunAuthorization Confirm(
        int expectedFavoriteCount,
        RequestSettings settings,
        IReadOnlyList<FavoriteAccountKey>? selectedAccounts = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (expectedFavoriteCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedFavoriteCount));
        }

        FavoriteAccountKey[] selected = selectedAccounts?.ToArray() ?? [];
        if (selected.Distinct().Count() != selected.Length ||
            (selected.Length > 0 && selected.Length != expectedFavoriteCount))
        {
            throw new ArgumentException("La selección autorizada no coincide con el total confirmado.", nameof(selectedAccounts));
        }

        return new(expectedFavoriteCount, settings with { }, selected);
    }

    internal bool TryConsume(
        IReadOnlyList<PamFavoriteAccount> observedAccounts,
        RequestSettings settings,
        out IReadOnlyList<PamFavoriteAccount> authorizedAccounts)
    {
        ArgumentNullException.ThrowIfNull(observedAccounts);
        authorizedAccounts = Array.Empty<PamFavoriteAccount>();
        if (confirmedSettings != settings || Interlocked.CompareExchange(ref consumed, 1, 0) != 0)
        {
            return false;
        }

        if (selectedAccounts.Count == 0)
        {
            if (observedAccounts.Count != ExpectedFavoriteCount) return false;
            authorizedAccounts = observedAccounts;
            return true;
        }

        var byKey = observedAccounts.ToDictionary(FavoriteAccountKey.From);
        PamFavoriteAccount[] selected = selectedAccounts
            .Where(byKey.ContainsKey)
            .Select(key => byKey[key])
            .ToArray();
        if (selected.Length != ExpectedFavoriteCount) return false;
        authorizedAccounts = selected;
        return true;
    }
}
