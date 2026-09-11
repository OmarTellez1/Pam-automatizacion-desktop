using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Infrastructure.Updates;

namespace PamAutomation.Tests.Infrastructure.Updates;

[TestClass]
public sealed class PublicManifestApplicationUpdateServiceTests
{
    private static readonly Uri ManifestUri = new("https://raw.githubusercontent.com/owner/repository/main/latest.json");
    private static readonly Uri RepositoryUri = new(
        "https://github.com/owner/repository");

    [TestMethod]
    public async Task CheckAsync_NewerValidatedVersion_ReturnsAvailable()
    {
        using var client = CreateClient(CreateManifest("0.0.2", RepositoryUri.AbsoluteUri));
        var service = new PublicManifestApplicationUpdateService(client, ManifestUri, RepositoryUri);

        var result = await service.CheckAsync(new Version(0, 0, 1, 0));

        Assert.IsTrue(result.IsUpdateAvailable);
        Assert.AreEqual(new Version(0, 0, 2, 0), result.LatestVersion);
    }

    [TestMethod]
    public async Task CheckAsync_SameVersion_ReturnsCurrent()
    {
        using var client = CreateClient(CreateManifest("0.0.1", RepositoryUri.AbsoluteUri));
        var service = new PublicManifestApplicationUpdateService(client, ManifestUri, RepositoryUri);

        var result = await service.CheckAsync(new Version(0, 0, 1, 0));

        Assert.IsFalse(result.IsUpdateAvailable);
    }

    [TestMethod]
    public async Task CheckAsync_UnexpectedRepository_RejectsManifest()
    {
        using var client = CreateClient(CreateManifest("0.0.2", "https://drive.google.com/drive/folders/other"));
        var service = new PublicManifestApplicationUpdateService(client, ManifestUri, RepositoryUri);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(new Version(0, 0, 1, 0)));
    }

    [TestMethod]
    public async Task CheckAsync_InvalidJson_RejectsManifest()
    {
        using var client = CreateClient("not-json");
        var service = new PublicManifestApplicationUpdateService(client, ManifestUri, RepositoryUri);

        await Assert.ThrowsAsync<JsonException>(
            () => service.CheckAsync(new Version(0, 0, 1, 0)));
    }

    [TestMethod]
    public async Task CheckAsync_OversizedContent_RejectsManifest()
    {
        using var client = CreateClient(new string('x', (16 * 1024) + 1));
        var service = new PublicManifestApplicationUpdateService(client, ManifestUri, RepositoryUri);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(new Version(0, 0, 1, 0)));
    }

    [TestMethod]
    public void Constructor_NonHttpsManifest_RejectsLocation()
    {
        using var client = CreateClient(string.Empty);

        Assert.ThrowsExactly<ArgumentException>(() => new PublicManifestApplicationUpdateService(
            client,
            new Uri("http://example.test/latest.json"),
            RepositoryUri));
    }

    private static HttpClient CreateClient(string content) => new(
        new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        }));

    private static string CreateManifest(string version, string repositoryUrl) => $$"""
        {
          "version": "{{version}}",
          "publishedAt": "2026-09-11",
          "repositoryUrl": "{{repositoryUrl}}"
        }
        """;

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
