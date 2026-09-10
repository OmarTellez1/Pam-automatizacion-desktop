using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Services;

public sealed record MailConfigurationEdit(MailSettings Settings, bool SendTestRequested);
