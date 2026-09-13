namespace PamAutomation.Core.Favorites;

/// <summary>Identifies controlled failures while reading PAM Favorites.</summary>
public enum PamFavoritesErrorCode
{
    /// <summary>The portal could not navigate to or expose Favorites.</summary>
    PortalUnavailable,
    /// <summary>Favorites did not contain any readable account.</summary>
    NoAccounts,
    /// <summary>At least one Favorites row had an incomplete identity.</summary>
    InvalidAccount,
    /// <summary>Favorites contained the same account identity more than once.</summary>
    DuplicateAccount,
}
