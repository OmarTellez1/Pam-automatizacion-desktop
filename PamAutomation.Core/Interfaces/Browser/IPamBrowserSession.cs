using PamAutomation.Core.Browser;

namespace PamAutomation.Core.Interfaces.Browser;

/// <summary>Represents an isolated browser session restricted to the PAM origin.</summary>
public interface IPamBrowserSession : IAsyncDisposable
{
    /// <summary>Navigates the session to an allowed PAM URL.</summary>
    /// <param name="uri">Absolute URL to navigate to.</param>
    /// <param name="cancellationToken">Cancels the navigation.</param>
    /// <returns>The non-sensitive response details needed by later phases.</returns>
    Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default);
}
