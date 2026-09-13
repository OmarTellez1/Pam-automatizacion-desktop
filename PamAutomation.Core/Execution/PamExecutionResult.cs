using System.Diagnostics;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;

namespace PamAutomation.Core.Execution;

/// <summary>Represents one completed execution while the application remains open.</summary>
[DebuggerDisplay("PAM execution result: {Mode}, {ProcessedCount} processed")]
public sealed record PamExecutionResult
{
    public PamExecutionResult(
        PamExecutionMode mode,
        DateTimeOffset completedAt,
        FinalAction finalAction,
        int favoriteCount,
        int processedCount,
        int conflictCount,
        IReadOnlyList<string> requestIds,
        OutlookDeliveryResult? mailDelivery = null)
    {
        ArgumentNullException.ThrowIfNull(requestIds);
        if (favoriteCount < 0 || processedCount < 0 || conflictCount < 0 ||
            processedCount > favoriteCount || conflictCount > processedCount)
        {
            throw new ArgumentOutOfRangeException(nameof(processedCount));
        }

        if (requestIds.Any(static id => string.IsNullOrWhiteSpace(id)) ||
            requestIds.Distinct(StringComparer.Ordinal).Count() != requestIds.Count ||
            requestIds.Count > processedCount - conflictCount)
        {
            throw new ArgumentException("Los Request ID deben ser completos y únicos.", nameof(requestIds));
        }

        Mode = mode;
        CompletedAt = completedAt;
        FinalAction = finalAction;
        FavoriteCount = favoriteCount;
        ProcessedCount = processedCount;
        ConflictCount = conflictCount;
        RequestIds = requestIds.ToArray();
        MailDelivery = mailDelivery;
    }

    public PamExecutionMode Mode { get; }

    public DateTimeOffset CompletedAt { get; }

    public FinalAction FinalAction { get; }

    public int FavoriteCount { get; }

    public int ProcessedCount { get; }

    public int ConflictCount { get; }

    public IReadOnlyList<string> RequestIds { get; }

    public OutlookDeliveryResult? MailDelivery { get; }

    public override string ToString() => $"PAM execution result: {Mode}, {ProcessedCount} processed";
}
