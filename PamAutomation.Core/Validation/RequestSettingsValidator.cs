using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Validation;

/// <summary>
/// Validates the non-sensitive values used to prepare a PAM request.
/// </summary>
public sealed class RequestSettingsValidator
{
    /// <summary>
    /// Validates request settings against the current date and time in Ecuador.
    /// </summary>
    /// <param name="settings">Settings to validate.</param>
    /// <param name="ecuadorNow">Current Ecuador date and time supplied by the caller.</param>
    /// <returns>A result containing every detected failure.</returns>
    public ConfigurationValidationResult Validate(
        RequestSettings? settings,
        DateTimeOffset ecuadorNow)
    {
        if (settings is null)
        {
            return ConfigurationValidationResult.From(
            [
                new(
                    ConfigurationValidationErrorCode.RequestSettingsRequired,
                    nameof(AppSettings.Request),
                    "La configuración de la solicitud es obligatoria.")
            ]);
        }

        var failures = new List<ConfigurationValidationFailure>();
        var currentDate = DateOnly.FromDateTime(ecuadorNow.DateTime);

        if (settings.StartDate < currentDate)
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.StartDateInPast,
                nameof(RequestSettings.StartDate),
                "La fecha de inicio no puede ser anterior a la fecha actual de Ecuador."));
        }
        else if (settings.StartDate == currentDate &&
                 settings.StartTime < TimeOnly.FromDateTime(ecuadorNow.DateTime))
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.StartTimeInPast,
                nameof(RequestSettings.StartTime),
                "La hora de inicio no puede estar en el pasado."));
        }

        if (settings.SessionHours is < 1 or > 8)
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.SessionHoursOutOfRange,
                nameof(RequestSettings.SessionHours),
                "La duración debe estar entre 1 y 8 horas."));
        }

        if (string.IsNullOrWhiteSpace(settings.RequestReason))
        {
            failures.Add(new(
                ConfigurationValidationErrorCode.RequestReasonRequired,
                nameof(RequestSettings.RequestReason),
                "La razón de la petición es obligatoria."));
        }

        return ConfigurationValidationResult.From(failures);
    }
}
