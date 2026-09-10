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
public sealed class PamRealPendingMatchTests
{
    private const string OptInVariable = "PAM_AUTOMATION_ALLOW_REAL_PENDING_MATCH";
    private const string RequiredOptInValue = "CREATE_ONE_AND_READ_PENDING";
    private const int MaximumSubmissionAttempts = 3;
    private static readonly TimeOnly AuthorizedStartTime = new(17, 5);

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Timeout(300_000)]
    public async Task SubmitAndMatchAsync_ExplicitAuthorization_UsesFixedFiveMinuteWindow()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(OptInVariable),
            RequiredOptInValue,
            StringComparison.Ordinal))
        {
            return;
        }

        PamCredentials? credentials = new WindowsPamCredentialStore().LoadForAuthentication();
        Assert.IsNotNull(credentials, "La prueba real requiere la credencial protegida configurada.");

        var authentication = new PamAuthenticationService(
            new PamBrowserSessionFactory(),
            new PamAuthenticationPortalFactory(),
            new PamHttpDateClockFactory(TimeProvider.System),
            new TotpGenerator(),
            new TaskAsyncDelay());
        var favorites = new PamFavoritesService(authentication, new PamFavoritesPortalFactory());
        string captureDirectory = Path.Combine(FindRepositoryRoot(), "img");
        Directory.CreateDirectory(captureDirectory);

        await using IPamBrowserSession session = await authentication.CreateAuthenticatedAsync(
            credentials,
            TestContext.CancellationToken);
        IReadOnlyList<PamFavoriteAccount> accounts = await favorites.ReadAllAsync(
            session,
            TestContext.CancellationToken);
        DateTimeOffset ecuadorNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            TimeProvider.System.GetUtcNow(),
            AppSettings.EcuadorTimeZoneId);
        DateOnly testDate = DateOnly.FromDateTime(ecuadorNow.DateTime);
        Assert.IsTrue(
            TimeOnly.FromDateTime(ecuadorNow.DateTime) < AuthorizedStartTime,
            "La ventana autorizada de hoy 05:05 PM ya comenzó; no se creó ninguna solicitud.");
        var form = new PamRequestFormData(
            testDate,
            AuthorizedStartTime,
            0,
            0,
            5,
            "Prueba tecnica autorizada Pam Automation fase 13 - 5 minutos");
        var pending = new PamPendingService(
            authentication,
            new PamPendingPortalFactory(captureDirectory),
            new TaskAsyncDelay());
        IReadOnlyList<PamPendingRequest> existingRequests = await pending.ReadAllAsync(
            session,
            TestContext.CancellationToken);
        foreach (PamPendingRequest request in existingRequests)
        {
            foreach (PamFavoriteAccount candidate in accounts.Take(MaximumSubmissionAttempts))
            {
                PamPendingComparisonResult existing = pending.Compare([request], [candidate], form);
                if (existing.IsComplete)
                {
                    TestContext.WriteLine(
                        "Real requests created: 0; existing authorized Pending match: 1; date: {0:yyyy-MM-dd}; start: {1}; expires: {2}; duration: 5 minutes.",
                        testDate,
                        AuthorizedStartTime.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                        AuthorizedStartTime.AddMinutes(5).ToString("hh:mm tt", CultureInfo.InvariantCulture));
                    return;
                }
            }
        }

        IPamRequestSubmissionPortal submission = new PamRequestSubmissionPortalFactory(
            captureDirectory: null,
            maximumSubmissionAttempts: MaximumSubmissionAttempts).Create(session);
        PamFavoriteAccount? target = null;
        int conflicts = 0;
        foreach (PamFavoriteAccount candidate in accounts.Take(MaximumSubmissionAttempts))
        {
            PamRequestPortalOutcome outcome = await submission.SubmitAsync(
                candidate,
                form,
                TestContext.CancellationToken);
            if (outcome.Status == PamRequestSubmissionStatus.Submitted)
            {
                target = candidate;
                break;
            }

            conflicts++;
        }

        Assert.IsNotNull(
            target,
            $"No se pudo crear la solicitud autorizada tras {conflicts} conflictos reconocidos.");

        PamPendingComparisonResult comparison = await pending.WaitForCompleteComparisonAsync(
            session,
            [target],
            form,
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(comparison.IsComplete, "La única solicitud creada no apareció en Pending.");
        Assert.AreEqual(1, comparison.MatchedCount);
        TestContext.WriteLine(
            "Real requests created: 1; Pending matches: 1; recognized conflicts before success: {0}; date: {1:yyyy-MM-dd}; start: {2}; expires: {3}; duration: 5 minutes.",
            conflicts,
            testDate,
            AuthorizedStartTime.ToString("hh:mm tt", CultureInfo.InvariantCulture),
            AuthorizedStartTime.AddMinutes(5).ToString("hh:mm tt", CultureInfo.InvariantCulture));
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
