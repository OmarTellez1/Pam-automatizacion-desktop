namespace PamAutomation.Browser;

/// <summary>Classifies non-sensitive accessible announcements emitted by the PAM login page.</summary>
internal static class PamLoginAlertClassifier
{
    /// <summary>Determines whether a visible login alert represents a credential rejection.</summary>
    public static bool IsCredentialRejection(string? alertText)
    {
        if (string.IsNullOrWhiteSpace(alertText))
        {
            return false;
        }

        return !string.Equals(
            alertText.Trim(),
            PamLoginSelectors.NavigationComplete,
            StringComparison.OrdinalIgnoreCase);
    }
}
