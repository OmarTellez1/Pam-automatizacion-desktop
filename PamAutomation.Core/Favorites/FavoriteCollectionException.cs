namespace PamAutomation.Core.Favorites;

/// <summary>Represents a controlled collection configuration or reconciliation failure.</summary>
public sealed class FavoriteCollectionException : Exception
{
    /// <summary>Initializes a safe collection failure.</summary>
    public FavoriteCollectionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
