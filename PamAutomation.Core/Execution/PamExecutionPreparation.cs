using System.Diagnostics;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Requests;

namespace PamAutomation.Core.Execution;

/// <summary>Contains the exact non-sensitive values displayed before a real run.</summary>
[DebuggerDisplay("PAM preparation: {FavoriteCount} Favorites rows")]
public sealed record PamExecutionPreparation
{
    /// <summary>Initializes the immutable snapshot used before a real PAM execution.</summary>
    /// <param name="favoriteCount">Number of Favorites rows selected for submission.</param>
    /// <param name="request">Validated request values.</param>
    /// <param name="finalAction">Action requested after reconciliation.</param>
    /// <param name="pendingRequestIdsBeforeSubmission">Request IDs already present in Pending.</param>
    /// <param name="exactPendingDuplicateCount">Number of exact requests already present in Pending.</param>
    /// <param name="reusablePendingRequestIds">Complete, unambiguous existing IDs reusable instead of submitting.</param>
    public PamExecutionPreparation(
        int favoriteCount,
        RequestSettings request,
        FinalAction finalAction,
        IReadOnlyList<string>? pendingRequestIdsBeforeSubmission = null,
        int exactPendingDuplicateCount = 0,
        IReadOnlyList<FavoriteAccountKey>? selectedAccounts = null,
        IReadOnlyList<string>? reusablePendingRequestIds = null)
    {
        if (favoriteCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(favoriteCount));
        }

        ArgumentNullException.ThrowIfNull(request);
        if (exactPendingDuplicateCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exactPendingDuplicateCount));
        }

        string[] baselineIds = pendingRequestIdsBeforeSubmission?.ToArray() ?? [];
        if (baselineIds.Any(static id => string.IsNullOrWhiteSpace(id)) ||
            baselineIds.Distinct(StringComparer.Ordinal).Count() != baselineIds.Length)
        {
            throw new ArgumentException(
                "Los Request ID existentes en Pending deben ser completos y únicos.",
                nameof(pendingRequestIdsBeforeSubmission));
        }

        FavoriteAccountKey[] selected = selectedAccounts?.ToArray() ?? [];
        if (selected.Any(static account => account is null) ||
            selected.Distinct().Count() != selected.Length ||
            (selected.Length > 0 && selected.Length != favoriteCount))
        {
            throw new ArgumentException(
                "La selección de Favorites debe ser completa, única y coincidir con el total confirmado.",
                nameof(selectedAccounts));
        }

        string[] reusableIds = reusablePendingRequestIds?.ToArray() ?? [];
        HashSet<string> baseline = baselineIds.ToHashSet(StringComparer.Ordinal);
        if (reusableIds.Any(static id => string.IsNullOrWhiteSpace(id)) ||
            reusableIds.Distinct(StringComparer.Ordinal).Count() != reusableIds.Length ||
            reusableIds.Any(id => !baseline.Contains(id)) ||
            (reusableIds.Length > 0 && reusableIds.Length != favoriteCount))
        {
            throw new ArgumentException(
                "Los Request ID reutilizables deben ser un conjunto completo, único y perteneciente a la línea base.",
                nameof(reusablePendingRequestIds));
        }

        FavoriteCount = favoriteCount;
        Request = request with { };
        FinalAction = finalAction;
        PendingRequestIdsBeforeSubmission = Array.AsReadOnly(baselineIds);
        ExactPendingDuplicateCount = exactPendingDuplicateCount;
        SelectedAccounts = Array.AsReadOnly(selected);
        ReusablePendingRequestIds = Array.AsReadOnly(reusableIds);
    }

    public int FavoriteCount { get; }

    public RequestSettings Request { get; }

    public FinalAction FinalAction { get; }

    /// <summary>Gets the Pending Request IDs observed before any submission.</summary>
    public IReadOnlyList<string> PendingRequestIdsBeforeSubmission { get; }

    /// <summary>Gets the number of existing Pending rows with the same account and request values.</summary>
    public int ExactPendingDuplicateCount { get; }

    /// <summary>Gets the exact collection identities authorized for this run; empty means all Favorites.</summary>
    public IReadOnlyList<FavoriteAccountKey> SelectedAccounts { get; }

    /// <summary>Gets a complete and unambiguous set of existing Pending IDs, or an empty list.</summary>
    public IReadOnlyList<string> ReusablePendingRequestIds { get; }

    /// <summary>Gets whether existing exact Pending rows require reuse-or-cancel handling.</summary>
    public bool HasExactPendingDuplicates => ExactPendingDuplicateCount > 0;

    /// <summary>Gets whether the existing Pending set can replace submission and be emailed safely.</summary>
    public bool CanReusePendingRequestIdsForEmail =>
        FinalAction == FinalAction.SendEmail && ReusablePendingRequestIds.Count == FavoriteCount;

    /// <summary>Ensures a reconciled result contains one new Request ID per submitted request.</summary>
    /// <param name="requestIds">Request IDs returned by the Pending reconciliation.</param>
    /// <param name="submittedCount">Number of requests confirmed as submitted.</param>
    public void ValidateReconciledRequestIds(IReadOnlyList<string> requestIds, int submittedCount)
    {
        ArgumentNullException.ThrowIfNull(requestIds);
        if (submittedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(submittedCount));
        }

        HashSet<string> baseline = PendingRequestIdsBeforeSubmission.ToHashSet(StringComparer.Ordinal);
        if (requestIds.Count != submittedCount || requestIds.Any(baseline.Contains))
        {
            throw new PamPendingException(PamPendingErrorCode.FieldMismatch);
        }
    }

    public override string ToString() => $"PAM preparation: {FavoriteCount} Favorites rows";
}
