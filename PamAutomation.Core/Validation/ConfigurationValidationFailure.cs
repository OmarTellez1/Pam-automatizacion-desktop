namespace PamAutomation.Core.Validation;

/// <summary>
/// Describes one invalid configuration value.
/// </summary>
/// <param name="Code">Stable identifier for the validation rule.</param>
/// <param name="PropertyName">Configuration property associated with the failure.</param>
/// <param name="Message">Human-readable explanation suitable for the user interface.</param>
public sealed record ConfigurationValidationFailure(
    ConfigurationValidationErrorCode Code,
    string PropertyName,
    string Message);
