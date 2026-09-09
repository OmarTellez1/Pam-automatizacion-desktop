using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Time;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;

namespace PamAutomation.Core.Authentication;

/// <summary>Coordinates the isolated, timing-safe PAM authentication workflow.</summary>
public sealed class PamAuthenticationService
{
    /// <summary>The sole portal origin allowed by the authentication workflow.</summary>
    public static readonly Uri AuthorizedOrigin = new("https://10.30.17.117");

    private readonly IPamBrowserSessionFactory browserSessionFactory;
    private readonly IPamAuthenticationPortalFactory portalFactory;
    private readonly IPamClockFactory clockFactory;
    private readonly TotpGenerator totpGenerator;
    private readonly IAsyncDelay delay;

    /// <summary>Initializes the service with its browser, portal and timing dependencies.</summary>
    public PamAuthenticationService(IPamBrowserSessionFactory browserSessionFactory, IPamAuthenticationPortalFactory portalFactory, IPamClockFactory clockFactory, TotpGenerator totpGenerator, IAsyncDelay delay)
    {
        this.browserSessionFactory = browserSessionFactory ?? throw new ArgumentNullException(nameof(browserSessionFactory));
        this.portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        this.clockFactory = clockFactory ?? throw new ArgumentNullException(nameof(clockFactory));
        this.totpGenerator = totpGenerator ?? throw new ArgumentNullException(nameof(totpGenerator));
        this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    /// <summary>Authenticates the supplied credentials with one permitted TOTP retry.</summary>
    public async Task AuthenticateAsync(PamCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();

        await using IPamBrowserSession session = await InvokePortalAsync(() => browserSessionFactory.CreateAsync(cancellationToken)).ConfigureAwait(false);
        IPamAuthenticationPortal portal = InvokePortal(() => portalFactory.Create(session));
        var navigation = await InvokePortalAsync(() => session.NavigateAsync(AuthorizedOrigin, cancellationToken)).ConfigureAwait(false);
        IClock clock = clockFactory.Create(navigation.HttpDateHeader, navigation.RoundTripTime);

        PamCredentialSubmissionResult credentialsResult = await InvokePortalAsync(() => portal.SubmitCredentialsAsync(credentials, cancellationToken)).ConfigureAwait(false);
        switch (credentialsResult)
        {
            case PamCredentialSubmissionResult.Accepted:
                break;
            case PamCredentialSubmissionResult.Rejected:
                throw new PamAuthenticationException(PamAuthenticationErrorCode.CredentialsRejected);
            default:
                throw new PamAuthenticationException(PamAuthenticationErrorCode.UnexpectedPortalResponse);
        }

        TotpCode firstCode = await CreateSafeCodeAsync(credentials.TotpSeed, clock, cancellationToken).ConfigureAwait(false);
        PamTotpSubmissionResult firstResult = await InvokePortalAsync(() => portal.SubmitTotpAsync(firstCode, cancellationToken)).ConfigureAwait(false);
        switch (firstResult)
        {
            case PamTotpSubmissionResult.Accepted:
                await ConfirmDashboardAsync(portal, cancellationToken).ConfigureAwait(false);
                return;
            case PamTotpSubmissionResult.Rejected:
                break;
            default:
                throw new PamAuthenticationException(PamAuthenticationErrorCode.UnexpectedPortalResponse);
        }

        await InvokePortalAsync(() => portal.WaitForTotpRejectionToDisappearAsync(cancellationToken)).ConfigureAwait(false);
        await WaitForNextIntervalAsync(firstCode, clock, cancellationToken).ConfigureAwait(false);
        TotpCode secondCode = await CreateSafeCodeAsync(credentials.TotpSeed, clock, cancellationToken).ConfigureAwait(false);
        PamTotpSubmissionResult secondResult = await InvokePortalAsync(() => portal.SubmitTotpAsync(secondCode, cancellationToken)).ConfigureAwait(false);
        switch (secondResult)
        {
            case PamTotpSubmissionResult.Accepted:
                await ConfirmDashboardAsync(portal, cancellationToken).ConfigureAwait(false);
                return;
            case PamTotpSubmissionResult.Rejected:
                throw new PamAuthenticationException(PamAuthenticationErrorCode.TotpRejected);
            default:
                throw new PamAuthenticationException(PamAuthenticationErrorCode.UnexpectedPortalResponse);
        }
    }

    private async Task<TotpCode> CreateSafeCodeAsync(string seed, IClock clock, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset referenceTime = clock.UtcNow;
            TotpCode code = totpGenerator.Generate(seed, referenceTime);
            if (code.HasMinimumRemainingLifetime(referenceTime, TotpGenerator.MinimumSubmissionLifetime))
            {
                return code;
            }

            await delay.DelayAsync(code.ExpiresAt - referenceTime, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForNextIntervalAsync(TotpCode code, IClock clock, CancellationToken cancellationToken)
    {
        TimeSpan remaining = code.ExpiresAt - clock.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            await delay.DelayAsync(remaining, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConfirmDashboardAsync(IPamAuthenticationPortal portal, CancellationToken cancellationToken)
    {
        if (!await InvokePortalAsync(() => portal.WaitForDashboardAsync(cancellationToken)).ConfigureAwait(false))
        {
            throw new PamAuthenticationException(PamAuthenticationErrorCode.ConfirmationMissing);
        }
    }

    private static async Task<T> InvokePortalAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (PamAuthenticationException) { throw; }
        catch (Exception exception)
        {
            throw new PamAuthenticationException(PamAuthenticationErrorCode.ConnectionOrCertificateFailure, exception);
        }
    }

    private static T InvokePortal<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (PamAuthenticationException) { throw; }
        catch (Exception exception)
        {
            throw new PamAuthenticationException(PamAuthenticationErrorCode.ConnectionOrCertificateFailure, exception);
        }
    }

    private static async Task InvokePortalAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            throw new PamAuthenticationException(PamAuthenticationErrorCode.ConnectionOrCertificateFailure, exception);
        }
    }
}
