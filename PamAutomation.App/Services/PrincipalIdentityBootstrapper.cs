using PamAutomation.Core.Accounts;
using PamAutomation.Core.Interfaces.Accounts;

namespace PamAutomation.App.Services;

/// <summary>Coordinates the idempotent creation of the principal identity metadata.</summary>
public sealed class PrincipalIdentityBootstrapper
{
    private readonly IPamAccountService accountService;

    /// <summary>Creates a bootstrapper for the protected PAM account registry.</summary>
    /// <param name="accountService">Account service that owns the registry and protected credential mappings.</param>
    public PrincipalIdentityBootstrapper(IPamAccountService accountService)
    {
        this.accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
    }

    /// <summary>Ensures that exactly one principal identity exists without modifying existing configuration.</summary>
    /// <param name="requestPrincipalName">Callback that requests a name, or returns <see langword="null"/> when cancelled.</param>
    /// <param name="cancellationToken">Token used to cancel protected storage operations.</param>
    /// <returns>The outcome of the first-run bootstrap.</returns>
    public async Task<PrincipalIdentityBootstrapResult> EnsureAsync(
        Func<string?> requestPrincipalName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestPrincipalName);
        IReadOnlyList<PamAccountStatus> accounts = await accountService.GetAccountsAsync(cancellationToken);
        if (accounts.Count > 0) return PrincipalIdentityBootstrapResult.Ready;

        string? name = requestPrincipalName();
        if (name is null) return PrincipalIdentityBootstrapResult.Cancelled;

        try
        {
            await accountService.CreatePrincipalAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (PamAccountException exception) when (exception.ErrorCode == PamAccountErrorCode.PrincipalAlreadyExists)
        {
            // Another in-process attempt completed after the initial read. The desired state is already present.
            return PrincipalIdentityBootstrapResult.Ready;
        }

        return PrincipalIdentityBootstrapResult.Created;
    }
}

/// <summary>Describes the result of the principal identity bootstrap.</summary>
public enum PrincipalIdentityBootstrapResult
{
    Ready,
    Created,
    Cancelled
}
