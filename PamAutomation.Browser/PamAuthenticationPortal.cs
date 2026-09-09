using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;

namespace PamAutomation.Browser;

/// <summary>Creates real PAM portal adapters bound to temporary browser sessions.</summary>
public sealed class PamAuthenticationPortalFactory : IPamAuthenticationPortalFactory
{
    /// <inheritdoc />
    public IPamAuthenticationPortal Create(IPamBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session is not PamBrowserSession pamSession)
        {
            throw new ArgumentException(
                "La autenticación real requiere una sesión creada por PamBrowserSessionFactory.",
                nameof(session));
        }

        return new PamAuthenticationPortal(pamSession.LoginPage);
    }
}

/// <summary>Maps the authentication workflow to the real PAM login page.</summary>
internal sealed class PamAuthenticationPortal(IPamLoginPage page) : IPamAuthenticationPortal
{
    private readonly IPamLoginPage page = page ?? throw new ArgumentNullException(nameof(page));

    public async Task<PamCredentialSubmissionResult> SubmitCredentialsAsync(
        PamCredentials credentials,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        bool accepted = await page.SubmitCredentialsAsync(
            credentials.Username,
            credentials.Password,
            cancellationToken).ConfigureAwait(false);

        return accepted ? PamCredentialSubmissionResult.Accepted : PamCredentialSubmissionResult.Rejected;
    }

    public async Task<PamTotpSubmissionResult> SubmitTotpAsync(
        TotpCode code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        bool accepted = await page.SubmitTotpAsync(code.Value, cancellationToken).ConfigureAwait(false);
        return accepted ? PamTotpSubmissionResult.Accepted : PamTotpSubmissionResult.Rejected;
    }

    public Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken) =>
        page.WaitForTotpRejectionToDisappearAsync(cancellationToken);

    public Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken) =>
        page.WaitForDashboardAsync(cancellationToken);
}
