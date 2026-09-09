using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Core.Interfaces.Requests;

/// <summary>Creates a safe request-test portal bound to an authenticated session.</summary>
public interface IPamRequestTestPortalFactory
{
    /// <summary>Creates the adapter. The returned contract has no submit operation.</summary>
    IPamRequestTestPortal Create(IPamBrowserSession session);
}
