using System.Text.Json.Serialization;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Infrastructure.Configuration;

internal sealed class AppSettingsDocument
{
    internal const string DisplayRequestIdsValue = "displayRequestIds";
    internal const string SendEmailValue = "sendEmail";

    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("finalAction")]
    public string? FinalAction { get; init; }

    [JsonPropertyName("requestReason")]
    public string? RequestReason { get; init; }

    [JsonPropertyName("sessionHours")]
    public int? SessionHours { get; init; }

    [JsonPropertyName("startDate")]
    public DateOnly? StartDate { get; init; }

    [JsonPropertyName("startTime")]
    public TimeOnly? StartTime { get; init; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; init; }

    [JsonPropertyName("testModeEnabled")]
    public bool? TestModeEnabled { get; init; }

    public static AppSettingsDocument FromDomain(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            throw CreateUnsupportedSchemaException(settings.SchemaVersion);
        }

        if (settings.Request is null)
        {
            throw CreateInvalidContentException("La configuración de solicitud es obligatoria.");
        }

        if (settings.Request.RequestReason is null || settings.TimeZoneId is null)
        {
            throw CreateInvalidContentException("La configuración contiene valores nulos no permitidos.");
        }

        return new AppSettingsDocument
        {
            SchemaVersion = settings.SchemaVersion,
            FinalAction = ToPersistedFinalAction(settings.FinalAction),
            RequestReason = settings.Request.RequestReason,
            SessionHours = settings.Request.SessionHours,
            StartDate = settings.Request.StartDate,
            StartTime = settings.Request.StartTime,
            TimeZone = settings.TimeZoneId,
            TestModeEnabled = settings.TestModeEnabled
        };
    }

    public AppSettings ToDomain()
    {
        if (SchemaVersion is null)
        {
            throw CreateInvalidContentException("La configuración no contiene schemaVersion.");
        }

        if (SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            throw CreateUnsupportedSchemaException(SchemaVersion.Value);
        }

        if (FinalAction is null ||
            RequestReason is null ||
            SessionHours is null ||
            StartDate is null ||
            StartTime is null ||
            TimeZone is null ||
            TestModeEnabled is null)
        {
            throw CreateInvalidContentException("La configuración está incompleta.");
        }

        return new AppSettings
        {
            SchemaVersion = SchemaVersion.Value,
            FinalAction = FromPersistedFinalAction(FinalAction),
            Request = new RequestSettings
            {
                RequestReason = RequestReason,
                SessionHours = SessionHours.Value,
                StartDate = StartDate.Value,
                StartTime = StartTime.Value
            },
            TimeZoneId = TimeZone,
            TestModeEnabled = TestModeEnabled.Value
        };
    }

    private static string ToPersistedFinalAction(FinalAction finalAction)
    {
        return finalAction switch
        {
            PamAutomation.Core.Models.Configuration.FinalAction.DisplayRequestIds => DisplayRequestIdsValue,
            PamAutomation.Core.Models.Configuration.FinalAction.SendEmail => SendEmailValue,
            _ => throw CreateInvalidContentException("La acción final no es válida.")
        };
    }

    private static FinalAction FromPersistedFinalAction(string finalAction)
    {
        return finalAction switch
        {
            DisplayRequestIdsValue => PamAutomation.Core.Models.Configuration.FinalAction.DisplayRequestIds,
            SendEmailValue => PamAutomation.Core.Models.Configuration.FinalAction.SendEmail,
            _ => throw CreateInvalidContentException("La acción final guardada no es válida.")
        };
    }

    private static AppSettingsPersistenceException CreateInvalidContentException(string message)
    {
        return new AppSettingsPersistenceException(
            AppSettingsPersistenceErrorCode.InvalidContent,
            message);
    }

    private static AppSettingsPersistenceException CreateUnsupportedSchemaException(int schemaVersion)
    {
        return new AppSettingsPersistenceException(
            AppSettingsPersistenceErrorCode.UnsupportedSchema,
            $"La versión de configuración {schemaVersion} no es compatible.");
    }
}
