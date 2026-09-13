using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Interfaces.Browser;

namespace PamAutomation.Browser;

/// <summary>Creates an automation adapter over the user's normal Microsoft Edge profile.</summary>
public sealed class PamInteractiveBrowserSessionFactory : IPamBrowserSessionFactory
{
    private readonly IPamCertificateTrustPrompt certificatePrompt;
    private readonly string edgeExecutablePath;
    private readonly ILogger<PamInteractiveBrowserSessionFactory> logger;

    /// <summary>Initializes the factory with the normal Edge installation.</summary>
    public PamInteractiveBrowserSessionFactory(IPamCertificateTrustPrompt certificatePrompt)
        : this(certificatePrompt, FindEdgeExecutable(), null)
    {
    }

    /// <summary>Initializes the factory with the normal Edge installation and structured logging.</summary>
    public PamInteractiveBrowserSessionFactory(
        IPamCertificateTrustPrompt certificatePrompt,
        ILogger<PamInteractiveBrowserSessionFactory>? logger)
        : this(certificatePrompt, FindEdgeExecutable(), logger)
    {
    }

    internal PamInteractiveBrowserSessionFactory(
        IPamCertificateTrustPrompt certificatePrompt,
        string edgeExecutablePath,
        ILogger<PamInteractiveBrowserSessionFactory>? logger = null)
    {
        this.certificatePrompt = certificatePrompt ?? throw new ArgumentNullException(nameof(certificatePrompt));
        this.edgeExecutablePath = File.Exists(edgeExecutablePath)
            ? Path.GetFullPath(edgeExecutablePath)
            : throw new FileNotFoundException("Microsoft Edge no está instalado en una ruta compatible.", edgeExecutablePath);
        this.logger = logger ?? NullLogger<PamInteractiveBrowserSessionFactory>.Instance;
    }

    /// <inheritdoc />
    public Task<IPamBrowserSession> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = new WindowsEdgePamPage(edgeExecutablePath, certificatePrompt, logger);
        var resources = new ExternalBrowserResources();
        IPamBrowserSession session = new PamBrowserSession(
            resources,
            resources,
            resources,
            page,
            new PamOriginPolicy(),
            prepareReleaseAsync: page.ReleaseAsync);
        return Task.FromResult(session);
    }

    private static string FindEdgeExecutable()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
        ];
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Microsoft Edge no está instalado en una ruta compatible.");
    }

    private sealed class ExternalBrowserResources : IPlaywrightEngine, IManagedBrowser, IManagedBrowserContext
    {
        public Task<IManagedBrowser> LaunchEdgeAsync(EdgeLaunchOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IManagedBrowserContext> NewContextAsync(PamBrowserContextOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task InstallRequestPolicyAsync(Func<string, bool> shouldAbortRequest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IManagedPage> NewPageAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
