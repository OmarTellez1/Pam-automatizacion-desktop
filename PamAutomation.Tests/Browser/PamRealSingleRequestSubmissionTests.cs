using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Browser;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Totp;
using PamAutomation.Infrastructure.Credentials;
using PamAutomation.Infrastructure.Time;

namespace PamAutomation.Tests.Browser;

[TestClass]
[TestCategory("ExplicitPamMutation")]
[DoNotParallelize]
public sealed class PamRealSingleRequestSubmissionTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_SINGLE_REAL_REQUEST";
    private const string RequiredOptInValue = "CREATE_ONE_REQUEST_TODAY";
    private const int StartMarginMinutes = 15;
    private const int VerificationDurationMinutes = 5;

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(300_000)]
    public async Task SubmitAsync_ExplicitSingleRequestAuthorization_CreatesExactlyOneRequestToday()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(OptInVariable),
            RequiredOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        PamCredentials? credentials = new WindowsPamCredentialStore().LoadForAuthentication();
        Assert.IsNotNull(
            credentials,
            "La verificación real requiere la credencial protegida configurada en Windows Credential Manager.");

        var authentication = new PamAuthenticationService(
            new PamBrowserSessionFactory(),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        var favorites = new PamFavoritesService(authentication, new PamFavoritesPortalFactory());

        TimeZoneInfo ecuadorTimeZone = TimeZoneInfo.FindSystemTimeZoneById(AppSettings.EcuadorTimeZoneId);
        DateTimeOffset ecuadorNow = TimeZoneInfo.ConvertTime(TimeProvider.System.GetUtcNow(), ecuadorTimeZone);
        DateTimeOffset start = RoundUpToFiveMinutes(ecuadorTime: ecuadorNow.AddMinutes(StartMarginMinutes));
        Assert.AreEqual(
            ecuadorNow.Date,
            start.Date,
            "No queda margen seguro para crear hoy la solicitud autorizada; no se intentó Submit Request.");

        var form = new PamRequestFormData(
            DateOnly.FromDateTime(ecuadorNow.DateTime),
            TimeOnly.FromDateTime(start.DateTime),
            durationDays: 0,
            durationHours: 0,
            durationMinutes: VerificationDurationMinutes,
            "Prueba tecnica autorizada Pam Automation - 5 minutos");

        string captureDirectory = Path.Combine(FindRepositoryRoot(), "img");
        Directory.CreateDirectory(captureDirectory);
        var submissionFactory = new PamRequestSubmissionPortalFactory(
            captureDirectory,
            maximumSubmissionAttempts: 1);

        await using IPamBrowserSession session = await authentication
            .CreateAuthenticatedAsync(credentials, TestContext.CancellationToken);
        IReadOnlyList<PamFavoriteAccount> accounts = await favorites
            .ReadAllAsync(session, TestContext.CancellationToken);
        PamFavoriteAccount target = accounts[0];
        IPamRequestSubmissionPortal portal = submissionFactory.Create(session);

        PamRequestPortalOutcome outcome = await portal.SubmitAsync(
            target,
            form,
            TestContext.CancellationToken);

        Assert.AreEqual(
            PamRequestSubmissionStatus.Submitted,
            outcome.Status,
            "La única Favorites row elegida produjo un conflicto; no se intentó otra fila.");
        TestContext.WriteLine(
            "Real requests created: 1; date: {0:yyyy-MM-dd}; time: {1} ({2} normalized); duration: {3} minutes.",
            start,
            start.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            start.ToString("HH:mm", CultureInfo.InvariantCulture),
            VerificationDurationMinutes);
    }

    private static DateTimeOffset RoundUpToFiveMinutes(DateTimeOffset ecuadorTime)
    {
        DateTimeOffset withoutSeconds = new(
            ecuadorTime.Year,
            ecuadorTime.Month,
            ecuadorTime.Day,
            ecuadorTime.Hour,
            ecuadorTime.Minute,
            0,
            ecuadorTime.Offset);
        int remainder = withoutSeconds.Minute % 5;
        return remainder == 0
            ? withoutSeconds
            : withoutSeconds.AddMinutes(5 - remainder);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PamAutomation.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio para guardar capturas.");
    }
}
