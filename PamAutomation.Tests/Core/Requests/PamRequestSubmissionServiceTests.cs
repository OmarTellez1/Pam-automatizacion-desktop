using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests.Core.Requests;

[TestClass]
[TestCategory("Unit")]
public sealed class PamRequestSubmissionServiceTests
{
    private static readonly DateTimeOffset EcuadorNow =
        new(2026, 9, 10, 8, 0, 0, TimeSpan.FromHours(-5));

    [TestMethod]
    public async Task ExecuteAsync_ConfirmedExactCount_ReturnsSubmittedAndConflictResults()
    {
        var harness = new SubmissionHarness(
        [
            PamRequestPortalOutcome.Submitted(),
            PamRequestPortalOutcome.ApprovedAccessOverlap(),
        ]);

        PamRequestRunResult result = await harness.Service.ExecuteAsync(
            CreateCredentials(),
            CreateSettings(),
            EcuadorNow,
            PamRequestRunAuthorization.Confirm(2, CreateSettings()));

        Assert.AreEqual(1, result.SubmittedCount);
        Assert.AreEqual(1, result.ConflictCount);
        Assert.HasCount(2, result.Accounts);
        Assert.HasCount(2, harness.SubmissionPortal.Forms);
        Assert.AreSame(harness.Session, harness.SubmissionFactory.ReceivedSession);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_KnownOutcomes_ReportsApplicationProgressInOrder()
    {
        var harness = new SubmissionHarness(
        [
            PamRequestPortalOutcome.Submitted(),
            PamRequestPortalOutcome.ApprovedAccessOverlap(),
        ]);
        var progress = new ProgressCollector<PamRequestSubmissionProgress>();

        await harness.Service.ExecuteAsync(
            CreateCredentials(),
            CreateSettings(),
            EcuadorNow,
            PamRequestRunAuthorization.Confirm(2, CreateSettings()),
            progress: progress);

        Assert.HasCount(2, progress.Values);
        Assert.AreEqual("application-a", progress.Values[0].Account.ApplicationName);
        Assert.AreEqual(PamRequestSubmissionStatus.Submitted, progress.Values[0].Status);
        Assert.AreEqual(1, progress.Values[0].ProcessedCount);
        Assert.AreEqual(2, progress.Values[1].ProcessedCount);
        Assert.AreEqual(2, progress.Values[1].TotalCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ObservedCountDiffersFromAuthorization_SubmitsNothing()
    {
        var harness = new SubmissionHarness([]);

        PamRequestSubmissionException exception =
            await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
                harness.Service.ExecuteAsync(
                    CreateCredentials(),
                    CreateSettings(),
                    EcuadorNow,
                    PamRequestRunAuthorization.Confirm(1, CreateSettings())));

        Assert.AreEqual(PamRequestSubmissionErrorCode.AuthorizationMismatch, exception.ErrorCode);
        Assert.AreEqual(0, harness.SubmissionFactory.CreateCalls);
        Assert.IsEmpty(harness.SubmissionPortal.Forms);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_SettingsChangedAfterAuthorization_SubmitsNothing()
    {
        var harness = new SubmissionHarness([]);
        RequestSettings confirmedSettings = CreateSettings();
        RequestSettings changedSettings = confirmedSettings with { RequestReason = "Changed after confirmation" };

        PamRequestSubmissionException exception =
            await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
                harness.Service.ExecuteAsync(
                    CreateCredentials(),
                    changedSettings,
                    EcuadorNow,
                    PamRequestRunAuthorization.Confirm(2, confirmedSettings)));

        Assert.AreEqual(PamRequestSubmissionErrorCode.AuthorizationMismatch, exception.ErrorCode);
        Assert.AreEqual(0, harness.SubmissionFactory.CreateCalls);
        Assert.IsEmpty(harness.SubmissionPortal.Forms);
        Assert.AreEqual(1, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthorizationReused_SubmitsOnlyFirstRun()
    {
        var harness = new SubmissionHarness(
        [
            PamRequestPortalOutcome.Submitted(),
            PamRequestPortalOutcome.Submitted(),
        ]);
        RequestSettings settings = CreateSettings();
        PamRequestRunAuthorization authorization = PamRequestRunAuthorization.Confirm(2, settings);

        PamRequestRunResult first = await harness.Service.ExecuteAsync(
            CreateCredentials(),
            settings,
            EcuadorNow,
            authorization);
        PamRequestSubmissionException exception =
            await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
                harness.Service.ExecuteAsync(
                    CreateCredentials(),
                    settings,
                    EcuadorNow,
                    authorization));

        Assert.AreEqual(2, first.SubmittedCount);
        Assert.AreEqual(PamRequestSubmissionErrorCode.AuthorizationMismatch, exception.ErrorCode);
        Assert.AreEqual(1, harness.SubmissionFactory.CreateCalls);
        Assert.HasCount(2, harness.SubmissionPortal.Forms);
        Assert.AreEqual(2, harness.Session.DisposeCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidSettings_DoesNotAuthenticate()
    {
        var harness = new SubmissionHarness([]);
        RequestSettings invalid = CreateSettings() with { SessionHours = 0 };

        PamRequestSubmissionException exception =
            await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
                harness.Service.ExecuteAsync(
                    CreateCredentials(),
                    invalid,
                    EcuadorNow,
                    PamRequestRunAuthorization.Confirm(2, invalid)));

        Assert.AreEqual(PamRequestSubmissionErrorCode.InvalidSettings, exception.ErrorCode);
        Assert.AreEqual(0, harness.AuthenticatedFactory.CreateCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAfterKnownOutcome_ReportsPartialProcessedCount()
    {
        var harness = new SubmissionHarness([PamRequestPortalOutcome.Submitted()]);

        PamRequestSubmissionException exception = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
            harness.Service.ExecuteAsync(
                CreateCredentials(), CreateSettings(), EcuadorNow,
                PamRequestRunAuthorization.Confirm(2, CreateSettings())));

        Assert.AreEqual(1, exception.ProcessedCount);
        Assert.AreEqual(PamRequestSubmissionErrorCode.PortalUnavailable, exception.ErrorCode);
    }

    [TestMethod]
    public void FormData_FiveMinutes_IsValidAndTextIsRedacted()
    {
        var form = new PamRequestFormData(
            new DateOnly(2026, 9, 10),
            new TimeOnly(9, 0),
            durationDays: 0,
            durationHours: 0,
            durationMinutes: 5,
            "sensitive business reason");

        Assert.AreEqual(5, form.TotalMinutes);
        Assert.AreEqual("PAM request form data (redacted)", form.ToString());
        Assert.DoesNotContain("sensitive", form.ToString());
    }

    [TestMethod]
    public void FormData_FromSettings_UsesHoursAndZeroMinutes()
    {
        PamRequestFormData form = PamRequestFormData.FromSettings(CreateSettings());

        Assert.AreEqual(0, form.DurationDays);
        Assert.AreEqual(2, form.DurationHours);
        Assert.AreEqual(0, form.DurationMinutes);
        Assert.AreEqual(120, form.TotalMinutes);
    }

    [TestMethod]
    [DataRow(-1, 0, 5)]
    [DataRow(0, -1, 5)]
    [DataRow(0, 0, 0)]
    [DataRow(0, 8, 1)]
    public void FormData_InvalidDuration_IsRejected(int days, int hours, int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PamRequestFormData(
            new DateOnly(2026, 9, 10),
            new TimeOnly(9, 0),
            days,
            hours,
            minutes,
            "Reason"));
    }

    [TestMethod]
    public void Authorization_NonPositiveCount_IsRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PamRequestRunAuthorization.Confirm(0, CreateSettings()));
    }

    private static PamCredentials CreateCredentials() =>
        new("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP");

    private static RequestSettings CreateSettings() => new()
    {
        StartDate = new DateOnly(2026, 9, 10),
        StartTime = new TimeOnly(9, 0),
        SessionHours = 2,
        RequestReason = "Authorized operational reason",
    };

    private sealed class SubmissionHarness
    {
        public SubmissionHarness(IReadOnlyList<PamRequestPortalOutcome> outcomes)
        {
            Session = new FakeSession();
            AuthenticatedFactory = new FakeAuthenticatedSessionFactory(Session);
            FavoritesFactory = new FakeFavoritesPortalFactory(new FakeFavoritesPortal(
            [
                new PamFavoriteAccount("system-a", "application-a", "account-a"),
                new PamFavoriteAccount("system-b", "application-b", "account-b"),
            ]));
            FavoritesService = new PamFavoritesService(AuthenticatedFactory, FavoritesFactory);
            SubmissionPortal = new FakeSubmissionPortal(outcomes);
            SubmissionFactory = new FakeSubmissionPortalFactory(SubmissionPortal);
            Service = new PamRequestSubmissionService(
                AuthenticatedFactory,
                FavoritesService,
                SubmissionFactory,
                new RequestSettingsValidator());
        }

        public FakeSession Session { get; }
        public FakeAuthenticatedSessionFactory AuthenticatedFactory { get; }
        public FakeFavoritesPortalFactory FavoritesFactory { get; }
        public PamFavoritesService FavoritesService { get; }
        public FakeSubmissionPortal SubmissionPortal { get; }
        public FakeSubmissionPortalFactory SubmissionFactory { get; }
        public PamRequestSubmissionService Service { get; }
    }

    private sealed class FakeAuthenticatedSessionFactory(IPamBrowserSession session)
        : IPamAuthenticatedSessionFactory
    {
        public int CreateCalls { get; private set; }

        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(session);
        }
    }

    private sealed class FakeFavoritesPortalFactory(IPamFavoritesPortal portal)
        : IPamFavoritesPortalFactory
    {
        public IPamFavoritesPortal Create(IPamBrowserSession session) => portal;
    }

    private sealed class FakeFavoritesPortal(IReadOnlyList<PamFavoriteAccount> accounts)
        : IPamFavoritesPortal
    {
        public Task<IReadOnlyList<PamFavoriteAccount>> ReadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(accounts);
    }

    private sealed class FakeSubmissionPortalFactory(IPamRequestSubmissionPortal portal)
        : IPamRequestSubmissionPortalFactory
    {
        public int CreateCalls { get; private set; }
        public IPamBrowserSession? ReceivedSession { get; private set; }

        public IPamRequestSubmissionPortal Create(IPamBrowserSession session)
        {
            CreateCalls++;
            ReceivedSession = session;
            return portal;
        }
    }

    private sealed class FakeSubmissionPortal(IReadOnlyList<PamRequestPortalOutcome> outcomes)
        : IPamRequestSubmissionPortal
    {
        private int index;
        public List<PamRequestFormData> Forms { get; } = [];

        public Task<PamRequestPortalOutcome> SubmitAsync(
            PamFavoriteAccount account,
            PamRequestFormData form,
            CancellationToken cancellationToken)
        {
            Forms.Add(form);
            return Task.FromResult(outcomes[index++]);
        }
    }

    private sealed class FakeSession : IPamBrowserSession
    {
        public int DisposeCalls { get; private set; }

        public Task<PamNavigationResponse> NavigateAsync(
            Uri uri,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ProgressCollector<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }
}
