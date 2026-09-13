using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Models.Credentials;

namespace PamAutomation.Browser;

/// <summary>Authenticates an externally owned Edge window and then releases automation control.</summary>
public sealed class PamInteractiveAccessService : IPamInteractiveAccessService
{
    private readonly IPamAuthenticatedSessionFactory authentication;
    private readonly ILogger<PamInteractiveAccessService> logger;
    private readonly SemaphoreSlim accessGate = new(1, 1);

    /// <summary>Initializes the interactive access workflow.</summary>
    /// <param name="authentication">Authentication flow backed by the dedicated interactive browser factory.</param>
    /// <param name="logger">Optional structured logger.</param>
    public PamInteractiveAccessService(
        IPamAuthenticatedSessionFactory authentication,
        ILogger<PamInteractiveAccessService>? logger = null)
    {
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        this.logger = logger ?? NullLogger<PamInteractiveAccessService>.Instance;
    }

    /// <inheritdoc />
    public async Task OpenAsync(PamCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IPamBrowserSession session = await authentication
                .CreateAuthenticatedAsync(credentials, cancellationToken)
                .ConfigureAwait(false);
            if (session is not IDetachablePamBrowserSession detachable)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException("La sesión interactiva de PAM no se puede liberar de forma segura.");
            }

            await detachable.DetachAsync().ConfigureAwait(false);
            logger.LogInformation("Interactive PAM browser authenticated and detached successfully.");
        }
        finally
        {
            accessGate.Release();
        }
    }
}
