namespace PamAutomation.Browser;

/// <summary>Validates URLs and request origins against the sole authorized PAM origin.</summary>
internal sealed class PamOriginPolicy
{
    /// <summary>The only origin the application may navigate to.</summary>
    public static readonly Uri AuthorizedOrigin = new("https://10.30.17.117");

    /// <summary>Determines whether a URI has the exact authorized scheme, host, and effective port.</summary>
    /// <param name="uri">The URI to validate.</param>
    /// <returns><see langword="true"/> only for the authorized origin.</returns>
    public bool IsAllowed(Uri? uri)
    {
        return uri is { IsAbsoluteUri: true }
            && string.Equals(uri.Scheme, AuthorizedOrigin.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, AuthorizedOrigin.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == AuthorizedOrigin.Port;
    }

    /// <summary>Determines whether an HTTP(S) request must be aborted by the context route.</summary>
    /// <param name="uri">The request URI.</param>
    /// <returns><see langword="true"/> when an HTTP(S) request targets another origin.</returns>
    public bool ShouldAbortRequest(Uri? uri)
    {
        return uri is { IsAbsoluteUri: true }
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            && !IsAllowed(uri);
    }

    /// <summary>Determines whether a routed request URL must be aborted.</summary>
    /// <param name="requestUrl">The raw URL supplied by Playwright routing.</param>
    /// <returns><see langword="true"/> for external or malformed HTTP(S) requests.</returns>
    internal bool ShouldAbortRequest(string requestUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestUrl);

        return Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri? uri)
            ? ShouldAbortRequest(uri)
            : HasHttpOrHttpsScheme(requestUrl);
    }

    private static bool HasHttpOrHttpsScheme(string url)
    {
        int separatorIndex = url.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        string scheme = url[..separatorIndex];
        return Uri.CheckSchemeName(scheme)
            && (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}
