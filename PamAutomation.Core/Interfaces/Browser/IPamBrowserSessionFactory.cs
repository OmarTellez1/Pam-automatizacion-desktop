namespace PamAutomation.Core.Interfaces.Browser;

/// <summary>Creates isolated, temporary sessions for the authorized PAM portal.</summary>
public interface IPamBrowserSessionFactory
{
    /// <summary>Creates a new browser session.</summary>
    /// <param name="cancellationToken">Cancels the asynchronous creation operation.</param>
    /// <returns>A session that must be asynchronously disposed.</returns>
    Task<IPamBrowserSession> CreateAsync(CancellationToken cancellationToken = default);
}
