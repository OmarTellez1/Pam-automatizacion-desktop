namespace PamAutomation.Core.Interfaces.Time;

/// <summary>
/// Provides the current trusted time in Coordinated Universal Time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current trusted time in Coordinated Universal Time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
