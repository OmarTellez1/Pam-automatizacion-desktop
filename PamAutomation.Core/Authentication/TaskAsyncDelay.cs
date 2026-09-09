using PamAutomation.Core.Interfaces.Authentication;

namespace PamAutomation.Core.Authentication;

/// <summary>Uses the platform asynchronous delay implementation for authentication waits.</summary>
public sealed class TaskAsyncDelay : IAsyncDelay
{
    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        return Task.Delay(delay, cancellationToken);
    }
}
