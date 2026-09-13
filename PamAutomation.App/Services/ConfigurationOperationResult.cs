namespace PamAutomation.App.Services;

/// <summary>Represents a safe user-facing configuration operation outcome.</summary>
public sealed record ConfigurationOperationResult(bool IsSuccess, string Message)
{
    /// <summary>Creates a successful outcome.</summary>
    public static ConfigurationOperationResult Success(string message) => new(true, message);

    /// <summary>Creates a failed outcome without exposing internal exception details.</summary>
    public static ConfigurationOperationResult Failure(string message) => new(false, message);
}
