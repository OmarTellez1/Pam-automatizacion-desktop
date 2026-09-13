namespace PamAutomation.Core.Browser;

/// <summary>Classifies controlled browser-session initialization failures.</summary>
public enum PamBrowserInitializationErrorCode
{
    /// <summary>Microsoft Edge could not be launched through the installed <c>msedge</c> channel.</summary>
    EdgeLaunchFailed,

    /// <summary>The Playwright runtime or temporary PAM session could not be initialized.</summary>
    SessionInitializationFailed,
}

/// <summary>Represents a controlled failure while creating a PAM browser session.</summary>
public sealed class PamBrowserInitializationException : Exception
{
    /// <summary>Initializes a controlled browser initialization error.</summary>
    /// <param name="code">Stable classification of the failed initialization stage.</param>
    public PamBrowserInitializationException(PamBrowserInitializationErrorCode code)
        : base(GetMessage(code))
    {
        Code = code;
    }

    /// <summary>Gets the stable classification of the failure.</summary>
    public PamBrowserInitializationErrorCode Code { get; }

    private static string GetMessage(PamBrowserInitializationErrorCode code) => code switch
    {
        PamBrowserInitializationErrorCode.EdgeLaunchFailed =>
            "No fue posible iniciar Microsoft Edge para la sesión PAM.",
        PamBrowserInitializationErrorCode.SessionInitializationFailed =>
            "No fue posible inicializar la sesión del navegador PAM.",
        _ => "No fue posible inicializar la sesión del navegador PAM.",
    };
}

/// <summary>Indicates that a URL does not belong to the authorized PAM origin.</summary>
public sealed class PamBrowserNavigationNotAllowedException : Exception
{
    /// <summary>Initializes a controlled unauthorized-navigation error.</summary>
    public PamBrowserNavigationNotAllowedException()
        : base("La navegación solicitada no pertenece al origen PAM autorizado.")
    {
    }
}
