using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Requests;

namespace PamAutomation.Browser;

/// <summary>Creates real request submission adapters bound to authenticated temporary sessions.</summary>
public sealed class PamRequestSubmissionPortalFactory : IPamRequestSubmissionPortalFactory
{
    private readonly string? captureDirectory;
    private readonly int? maximumSubmissionAttempts;

    /// <summary>Initializes the production factory without development screenshots or an artificial limit.</summary>
    public PamRequestSubmissionPortalFactory()
    {
    }

    internal PamRequestSubmissionPortalFactory(
        string? captureDirectory,
        int? maximumSubmissionAttempts)
    {
        if (captureDirectory is not null && !Path.IsPathFullyQualified(captureDirectory))
        {
            throw new ArgumentException("La carpeta de capturas debe ser absoluta.", nameof(captureDirectory));
        }

        if (maximumSubmissionAttempts is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSubmissionAttempts));
        }

        this.captureDirectory = captureDirectory;
        this.maximumSubmissionAttempts = maximumSubmissionAttempts;
    }

    /// <inheritdoc />
    public IPamRequestSubmissionPortal Create(IPamBrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session is not PamBrowserSession pamSession)
        {
            throw new ArgumentException(
                "Las solicitudes reales requieren una sesión creada por PamBrowserSessionFactory.",
                nameof(session));
        }

        return new PamRequestSubmissionPortal(
            pamSession.RequestSubmissionPage,
            captureDirectory,
            maximumSubmissionAttempts);
    }
}

/// <summary>Maps the real submission contract to one authenticated page.</summary>
internal sealed class PamRequestSubmissionPortal(
    IPamRequestSubmissionPage page,
    string? captureDirectory,
    int? maximumSubmissionAttempts) : IPamRequestSubmissionPortal
{
    private readonly IPamRequestSubmissionPage page = page ?? throw new ArgumentNullException(nameof(page));

    public Task<PamRequestPortalOutcome> SubmitAsync(
        PamFavoriteAccount account,
        PamRequestFormData form,
        CancellationToken cancellationToken) =>
        page.SubmitRequestFormAsync(
            account,
            form,
            captureDirectory,
            maximumSubmissionAttempts,
            cancellationToken);
}
