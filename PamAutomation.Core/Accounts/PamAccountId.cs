namespace PamAutomation.Core.Accounts;

/// <summary>Identifies one PAM account independently from its visible name.</summary>
public readonly record struct PamAccountId(Guid Value)
{
    /// <summary>Creates a new random account identity.</summary>
    public static PamAccountId New() => new(Guid.NewGuid());

    /// <summary>Returns the stable lowercase storage key.</summary>
    public string ToStorageKey() => Value.ToString("N");
}
