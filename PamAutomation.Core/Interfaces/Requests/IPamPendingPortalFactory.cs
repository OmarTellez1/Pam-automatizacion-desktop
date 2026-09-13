using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Core.Interfaces.Requests;

/// <summary>Creates a <c>Pending</c> reader bound to an authenticated PAM session.</summary>
public interface IPamPendingPortalFactory
{
    /// <summary>Creates the reader for the supplied session.</summary>
    IPamPendingPortal Create(IPamBrowserSession session);
}
