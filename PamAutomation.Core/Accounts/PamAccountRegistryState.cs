namespace PamAutomation.Core.Accounts;

/// <summary>Contains the complete non-sensitive registry of PAM identities.</summary>
public sealed record PamAccountRegistryState
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumAccounts = 9;

    public PamAccountRegistryState(IReadOnlyList<PamAccountSummary>? accounts = null)
    {
        PamAccountSummary[] copied = accounts?.ToArray() ?? [];
        if (copied.Length > MaximumAccounts ||
            copied.Select(account => account.Id).Distinct().Count() != copied.Length ||
            copied.Select(account => account.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != copied.Length ||
            copied.Count(account => account.IsPrimary) is > 1 ||
            (copied.Length > 0 && copied.Count(account => account.IsPrimary) != 1))
            throw new ArgumentException("El registro de cuentas PAM no es válido.", nameof(accounts));
        Accounts = Array.AsReadOnly(copied);
    }

    public IReadOnlyList<PamAccountSummary> Accounts { get; }
    public PamAccountSummary? PrimaryAccount => Accounts.FirstOrDefault(account => account.IsPrimary);
}
