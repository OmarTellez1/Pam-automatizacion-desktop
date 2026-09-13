using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.App.Services;

public sealed record RequestConfigurationEdit(RequestSettings Request, bool TestModeEnabled);
