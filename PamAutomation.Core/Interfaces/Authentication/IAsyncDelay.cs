namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Provides cancellable asynchronous waiting for authentication timing rules.</summary>
public interface IAsyncDelay
{
    /// <summary>Waits for the requested duration without blocking a thread.</summary>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
