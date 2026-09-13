namespace PamAutomation.Core.Browser;

/// <summary>Non-sensitive details returned after a PAM navigation completes.</summary>
/// <param name="Uri">The final response URI.</param>
/// <param name="StatusCode">The HTTP status code, when a response was received.</param>
/// <param name="HttpDateHeader">The optional HTTP <c>Date</c> header value.</param>
/// <param name="RoundTripTime">The monotonic elapsed time from navigation start until its response was received.</param>
public sealed record PamNavigationResponse(
    Uri Uri,
    int? StatusCode,
    string? HttpDateHeader,
    TimeSpan RoundTripTime);
