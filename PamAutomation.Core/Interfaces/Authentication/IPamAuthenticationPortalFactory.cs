using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Core.Interfaces.Authentication;

/// <summary>Creates portal interactions bound to one isolated PAM browser session.</summary>
public interface IPamAuthenticationPortalFactory
{
    /// <summary>Creates the portal adapter for the supplied temporary PAM session.</summary>
    /// <param name="session">The isolated browser session that owns the portal page.</param>
    /// <returns>A portal adapter bound exclusively to <paramref name="session"/>.</returns>
    IPamAuthenticationPortal Create(IPamBrowserSession session);
}
