using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PamAutomation.Core.Interfaces.Updates;
using PamAutomation.Core.Updates;

namespace PamAutomation.Infrastructure.Updates;

/// <summary>Reads a small release manifest from a trusted public HTTPS endpoint.</summary>
public sealed class PublicManifestApplicationUpdateService : IApplicationUpdateService
{
    private const int MaximumManifestBytes = 16 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly HttpClient httpClient;
    private readonly Uri manifestUri;
    private readonly Uri repositoryUri;
    private readonly ILogger<PublicManifestApplicationUpdateService> logger;

    /// <summary>Initializes the public manifest reader.</summary>
    /// <param name="httpClient">HTTP client used for the bounded remote request.</param>
    /// <param name="manifestUri">Direct HTTPS URI of the public manifest.</param>
    /// <param name="repositoryUri">Exact public repository URI expected inside the manifest.</param>
    /// <param name="logger">Optional safe diagnostic logger.</param>
    public PublicManifestApplicationUpdateService(
        HttpClient httpClient,
        Uri manifestUri,
        Uri repositoryUri,
        ILogger<PublicManifestApplicationUpdateService>? logger = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.manifestUri = ValidateHttpsUri(manifestUri, nameof(manifestUri));
        this.repositoryUri = ValidateHttpsUri(repositoryUri, nameof(repositoryUri));
        this.logger = logger ?? NullLogger<PublicManifestApplicationUpdateService>.Instance;
    }

    /// <inheritdoc />
    public async Task<ApplicationUpdateResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        logger.LogInformation("Application update manifest check started.");

        using HttpResponseMessage response = await httpClient
            .GetAsync(manifestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        byte[] content = await ReadBoundedContentAsync(response.Content, cancellationToken).ConfigureAwait(false);
        UpdateManifest? manifest = JsonSerializer.Deserialize<UpdateManifest>(content, SerializerOptions);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.RepositoryUrl))
        {
            throw new InvalidDataException("El manifiesto de actualización no contiene los campos requeridos.");
        }

        if (!Version.TryParse(manifest.Version, out Version? latestVersion) ||
            latestVersion.Major < 0 || latestVersion.Minor < 0)
        {
            throw new InvalidDataException("El manifiesto de actualización contiene una versión inválida.");
        }

        if (!Uri.TryCreate(manifest.RepositoryUrl, UriKind.Absolute, out Uri? publishedRepositoryUri) ||
            Uri.Compare(
                publishedRepositoryUri,
                repositoryUri,
                UriComponents.AbsoluteUri,
                UriFormat.SafeUnescaped,
                StringComparison.Ordinal) != 0)
        {
            throw new InvalidDataException("El manifiesto de actualización apunta a un repositorio inesperado.");
        }

        Version normalizedCurrent = NormalizeVersion(currentVersion);
        Version normalizedLatest = NormalizeVersion(latestVersion);
        bool isAvailable = normalizedLatest > normalizedCurrent;
        logger.LogInformation(
            "Application update manifest check completed. Status={Status}.",
            isAvailable ? "Available" : "Current");
        return new ApplicationUpdateResult(normalizedLatest, isAvailable);
    }

    private static Uri ValidateHttpsUri(Uri uri, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(uri, parameterName);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("La ubicación debe ser una dirección HTTPS sin credenciales.", parameterName);
        }

        return uri;
    }

    private static async Task<byte[]> ReadBoundedContentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumManifestBytes)
        {
            throw new InvalidDataException("El manifiesto de actualización excede el tamaño permitido.");
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream();
        byte[] buffer = new byte[4096];
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (destination.Length + read > MaximumManifestBytes)
            {
                throw new InvalidDataException("El manifiesto de actualización excede el tamaño permitido.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return destination.ToArray();
    }

    private static Version NormalizeVersion(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    private sealed record UpdateManifest(
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("publishedAt")] string? PublishedAt,
        [property: JsonPropertyName("repositoryUrl")] string? RepositoryUrl);
}
