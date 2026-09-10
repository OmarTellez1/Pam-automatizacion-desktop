namespace PamAutomation.Core.Outlook;

/// <summary>Identifies a safe failure while sanitizing or composing Outlook HTML.</summary>
public enum OutlookHtmlCompositionErrorCode
{
    /// <summary>The configured HTML template is absent.</summary>
    MissingTemplate,

    /// <summary>The configured HTML template exceeds the supported size.</summary>
    TemplateTooLarge,

    /// <summary>The configured template is not well-formed XHTML.</summary>
    InvalidMarkup,

    /// <summary>The configured template does not contain the request identifier marker.</summary>
    MissingRequestIdsMarker,

    /// <summary>The configured template contains the request identifier marker more than once.</summary>
    MultipleRequestIdsMarkers,

    /// <summary>No request identifiers were supplied for composition.</summary>
    MissingRequestIds,

    /// <summary>A supplied request identifier is not a valid numeric PAM identifier.</summary>
    InvalidRequestId,

    /// <summary>A supplied request identifier occurs more than once.</summary>
    DuplicateRequestId,

    /// <summary>The final body differs from its sanitized representation.</summary>
    UnsafeComposedBody,

    /// <summary>An expected request identifier is absent from the final body.</summary>
    MissingComposedRequestId,
}
