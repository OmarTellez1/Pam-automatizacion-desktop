using System.Diagnostics;

namespace PamAutomation.Core.Requests;

/// <summary>Summarizes an in-memory request test run without account identities.</summary>
[DebuggerDisplay("PAM request test: {ValidatedCount} validated, {SubmittedCount} submitted")]
public sealed record PamRequestTestResult(int FavoriteCount, int ValidatedCount)
{
    /// <summary>Gets the number of submitted requests, which is invariantly zero in test mode.</summary>
    public int SubmittedCount => 0;

    /// <inheritdoc />
    public override string ToString() =>
        $"PAM request test: {ValidatedCount} validated, {SubmittedCount} submitted";
}
