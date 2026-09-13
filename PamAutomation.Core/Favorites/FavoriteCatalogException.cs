namespace PamAutomation.Core.Favorites;

/// <summary>Represents a safe failure while loading or refreshing the local Favorites catalog.</summary>
public sealed class FavoriteCatalogException : Exception
{
    /// <summary>Initializes a safe catalog failure.</summary>
    /// <param name="message">User-safe explanation.</param>
    /// <param name="innerException">Optional technical cause retained only for diagnostics.</param>
    public FavoriteCatalogException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
