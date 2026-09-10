using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Core.Interfaces.Requests;

/// <summary>Creates a real submission portal bound to an authenticated PAM session.</summary>
public interface IPamRequestSubmissionPortalFactory
{
    /// <summary>Creates the session-bound portal.</summary>
    /// <param name="session">Authenticated session owned by the caller.</param>
    /// <returns>A portal that can perform explicitly authorized submissions.</returns>
    IPamRequestSubmissionPortal Create(IPamBrowserSession session);
}
