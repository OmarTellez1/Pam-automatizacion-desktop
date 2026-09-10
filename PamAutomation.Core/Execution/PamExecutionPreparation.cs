using System.Diagnostics;
using PamAutomation.Core.Models.Configuration;

namespace PamAutomation.Core.Execution;

/// <summary>Contains the exact non-sensitive values displayed before a real run.</summary>
[DebuggerDisplay("PAM preparation: {FavoriteCount} Favorites rows")]
public sealed record PamExecutionPreparation
{
    public PamExecutionPreparation(int favoriteCount, RequestSettings request, FinalAction finalAction)
    {
        if (favoriteCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(favoriteCount));
        }

        ArgumentNullException.ThrowIfNull(request);
        FavoriteCount = favoriteCount;
        Request = request with { };
        FinalAction = finalAction;
    }

    public int FavoriteCount { get; }

    public RequestSettings Request { get; }

    public FinalAction FinalAction { get; }

    public override string ToString() => $"PAM preparation: {FavoriteCount} Favorites rows";
}
