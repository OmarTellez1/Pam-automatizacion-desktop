namespace PamAutomation.Core.Updates;

/// <summary>Describes the latest validated portable application release.</summary>
/// <param name="LatestVersion">Latest version published by the owner.</param>
/// <param name="IsUpdateAvailable">Whether the latest version is newer than the running version.</param>
public sealed record ApplicationUpdateResult(Version LatestVersion, bool IsUpdateAvailable);
