using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Validation;

/// <summary>
/// Validates the complete non-sensitive application configuration.
/// </summary>
/// <param name="requestSettingsValidator">Validator for the nested request settings.</param>
public sealed class AppSettingsValidator(RequestSettingsValidator requestSettingsValidator)
{
    private readonly RequestSettingsValidator _requestSettingsValidator =
        requestSettingsValidator ?? throw new ArgumentNullException(nameof(requestSettingsValidator));

    /// <summary>
    /// Validates application settings against the current date and time in Ecuador.
    /// </summary>
    /// <param name="settings">Settings to validate.</param>
    /// <param name="ecuadorNow">Current Ecuador date and time supplied by the caller.</param>
    /// <returns>A result containing every detected failure.</returns>
    public ConfigurationValidationResult Validate(
        AppSettings? settings,
        DateTimeOffset ecuadorNow)
    {
        if (settings is null)
        {
            return ConfigurationValidationResult.From(
            [
                new(
                    ConfigurationValidationErrorCode.SettingsRequired,
                    nameof(AppSettings),
                    "La configuración de la aplicación es obligatoria.")
            ]);
        }

        var failures = new List<ConfigurationValidationFailure>();

        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.SchemaVersionUnsupported,
                nameof(AppSettings.SchemaVersion),
                "La versión de la configuración no es compatible con esta versión de la aplicación."));
        }

        if (!Enum.IsDefined(settings.FinalAction))
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.FinalActionUnsupported,
                nameof(AppSettings.FinalAction),
                "La acción final configurada no es válida."));
        }

        if (!string.Equals(
                settings.TimeZoneId,
                AppSettings.EcuadorTimeZoneId,
                StringComparison.Ordinal))
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.TimeZoneUnsupported,
                nameof(AppSettings.TimeZoneId),
                "La zona horaria debe ser America/Guayaquil."));
        }

        failures.AddRange(_requestSettingsValidator.Validate(settings.Request, ecuadorNow).Failures);

        return ConfigurationValidationResult.From(failures);
    }
}
