using System.Collections.ObjectModel;

namespace PamAutomation.Core.Validation;

/// <summary>
/// Represents the result of validating non-sensitive application configuration.
/// </summary>
public sealed class ConfigurationValidationResult
{
    private static readonly ConfigurationValidationResult SuccessfulResult =
        new(Array.Empty<ConfigurationValidationFailure>());

    private readonly ReadOnlyCollection<ConfigurationValidationFailure> failures;

    internal ConfigurationValidationResult(IEnumerable<ConfigurationValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        this.failures = Array.AsReadOnly(failures.ToArray());
    }

    /// <summary>
    /// Gets a successful validation result with no failures.
    /// </summary>
    public static ConfigurationValidationResult Success => SuccessfulResult;

    /// <summary>
    /// Gets a value indicating whether every validation rule passed.
    /// </summary>
    public bool IsValid => failures.Count == 0;

    /// <summary>
    /// Gets the complete immutable collection of validation failures.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationFailure> Failures => failures;

    internal static ConfigurationValidationResult From(
        IEnumerable<ConfigurationValidationFailure> failures)
    {
        var result = new ConfigurationValidationResult(failures);
        return result.IsValid ? Success : result;
    }
}
