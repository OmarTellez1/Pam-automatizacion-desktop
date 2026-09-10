using System.Text.Json;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

/// <summary>Classifies only the PAM submission responses explicitly understood by the application.</summary>
internal static class PamSubmissionResponseClassifier
{
    public static PamRequestPortalOutcome Classify(int status, string? payload)
    {
        if (status is >= 200 and < 300)
        {
            return PamRequestPortalOutcome.Submitted();
        }

        string? code = ReadErrorCode(payload);
        if (status == 400 &&
            string.Equals(
                code,
                PamRequestConflictCodes.ApprovedAccessOverlap,
                StringComparison.Ordinal))
        {
            return PamRequestPortalOutcome.ApprovedAccessOverlap();
        }

        throw new PamRequestSubmissionException(PamRequestSubmissionErrorCode.RequestRejected);
    }

    private static string? ReadErrorCode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("ErrorDetails", out JsonElement details) &&
                details.ValueKind == JsonValueKind.Object &&
                details.TryGetProperty("ErrorCode", out JsonElement errorCode) &&
                errorCode.ValueKind == JsonValueKind.Object &&
                errorCode.TryGetProperty("Key", out JsonElement key) &&
                key.ValueKind == JsonValueKind.String)
            {
                return key.GetString();
            }
        }
        catch (JsonException)
        {
            // Unknown payloads remain rejected without exposing their contents.
        }

        return null;
    }
}
