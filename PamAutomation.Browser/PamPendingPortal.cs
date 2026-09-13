using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

/// <summary>Creates read-only <c>Pending</c> adapters bound to authenticated temporary sessions.</summary>
public sealed class PamPendingPortalFactory : IPamPendingPortalFactory
{
    private readonly string? captureDirectory;

    /// <summary>Initializes the production reader without development screenshots.</summary>
    public PamPendingPortalFactory()
    {
    }

    internal PamPendingPortalFactory(string? captureDirectory)
    {
        if (captureDirectory is not null && !Path.IsPathFullyQualified(captureDirectory))
        {
            throw new ArgumentException("La carpeta de capturas debe ser absoluta.", nameof(captureDirectory));
        }

        this.captureDirectory = captureDirectory;
    }

    /// <inheritdoc />
    public IPamPendingPortal Create(IPamBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session is not PamBrowserSession pamSession)
        {
            throw new ArgumentException(
                "Pending requiere una sesión creada por PamBrowserSessionFactory.",
                nameof(session));
        }

        return new PamPendingPortal(pamSession.PendingPage, captureDirectory);
    }
}

internal sealed class PamPendingPortal(IPamPendingPage page, string? captureDirectory) : IPamPendingPortal
{
    private readonly IPamPendingPage page = page ?? throw new ArgumentNullException(nameof(page));

    public Task<IReadOnlyList<PamPendingRequest>> ReadAllAsync(CancellationToken cancellationToken) =>
        page.ReadAllPendingAsync(captureDirectory, cancellationToken);
}
