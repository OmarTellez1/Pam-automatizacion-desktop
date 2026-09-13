using System.Text.RegularExpressions;

namespace PamAutomation.Browser;

/// <summary>Centralizes the stable accessible names observed in the real PAM login.</summary>
internal static partial class PamLoginSelectors
{
    public const string Username = "Username";
    public const string Password = "Password";
    public const string LogIn = "Log In";
    public const string EnterResponse = "Enter Response";
    public const string Submit = "Submit";
    public const string ReturnToDashboard = "Return to dashboard";
    public const string NavigationComplete = "Navigation complete.";

    [GeneratedRegex("the provided token was rejected", RegexOptions.IgnoreCase)]
    public static partial Regex TokenRejectedExpression();
}
