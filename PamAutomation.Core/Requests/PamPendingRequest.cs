using System.Diagnostics;

namespace PamAutomation.Core.Requests;

/// <summary>Represents one validated row from the PAM <c>Pending</c> grid.</summary>
[DebuggerDisplay("PAM Pending request (redacted)")]
public sealed record PamPendingRequest
{
    /// <summary>Initializes one complete <c>Pending</c> row.</summary>
    public PamPendingRequest(
        string requestId,
        string systemName,
        string applicationName,
        string accountName,
        DateOnly startDate,
        TimeOnly startTime,
        DateOnly expiresDate,
        TimeOnly expiresTime,
        string responseStatus,
        string? description)
    {
        string normalizedRequestId = RequireValue(requestId, nameof(requestId));
        if (!normalizedRequestId.All(static character => character is >= '0' and <= '9'))
        {
            throw new ArgumentException("Request ID debe contener únicamente dígitos.", nameof(requestId));
        }

        RequestId = normalizedRequestId;
        SystemName = RequireValue(systemName, nameof(systemName));
        ApplicationName = RequireValue(applicationName, nameof(applicationName));
        AccountName = RequireValue(accountName, nameof(accountName));
        StartDate = startDate;
        StartTime = startTime;
        ExpiresDate = expiresDate;
        ExpiresTime = expiresTime;
        string normalizedStatus = RequireValue(responseStatus, nameof(responseStatus));
        if (!string.Equals(normalizedStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La fila no pertenece al estado Pending.", nameof(responseStatus));
        }

        Description = description?.Trim() ?? string.Empty;
    }

    /// <summary>Gets the numeric PAM Request ID.</summary>
    public string RequestId { get; }

    /// <summary>Gets the system displayed by PAM.</summary>
    public string SystemName { get; }

    /// <summary>Gets the application displayed by PAM.</summary>
    public string ApplicationName { get; }

    /// <summary>Gets the account displayed by PAM.</summary>
    public string AccountName { get; }

    /// <summary>Gets the request start date in Ecuador.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Gets the request start time.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Gets the expiration date in Ecuador.</summary>
    public DateOnly ExpiresDate { get; }

    /// <summary>Gets the expiration time.</summary>
    public TimeOnly ExpiresTime { get; }

    /// <summary>Gets the description shown by PAM.</summary>
    public string Description { get; }

    /// <summary>Gets the selected portal status.</summary>
    public string Status => "Pending";

    /// <inheritdoc />
    public override string ToString() => "PAM Pending request (redacted)";

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La fila Pending está incompleta.", parameterName);
        }

        return value.Trim();
    }
}
