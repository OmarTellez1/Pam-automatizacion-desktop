using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Authentication;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Time;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Totp;

namespace PamAutomation.Tests.Core.Authentication;

[TestClass]
[TestCategory("Unit")]
public sealed class PamAuthenticationServiceTests
{
    private const string Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [TestMethod]
    public async Task AuthenticateAsync_AcceptedCredentialsAndTotp_CompletesAfterDashboardConfirmation()
    {
        // Arrange
        var harness = new AuthenticationHarness();

        // Act
        await harness.Service.AuthenticateAsync(CreateCredentials());

        // Assert
        Assert.AreEqual(PamAuthenticationService.LoginUri, harness.Session.NavigatedUri);
        Assert.AreEqual(1, harness.Portal.CredentialCalls);
        Assert.AreEqual(1, harness.Portal.TotpCalls);
        Assert.AreEqual(1, harness.Portal.DashboardCalls);
        Assert.AreSame(harness.Session, harness.PortalFactory.ReceivedSession);
        Assert.AreEqual(harness.Session.Response.HttpDateHeader, harness.ClockFactory.ReceivedHttpDateHeader);
        Assert.AreEqual(harness.Session.Response.RoundTripTime, harness.ClockFactory.ReceivedRoundTripTime);
    }

    [TestMethod]
    public async Task AuthenticateAsync_LessThanEightSecondsRemaining_WaitsForFreshSafeWindow()
    {
        // Arrange
        var harness = new AuthenticationHarness(DateTimeOffset.UnixEpoch.AddSeconds(23));

        // Act
        await harness.Service.AuthenticateAsync(CreateCredentials());

        // Assert
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(7) }, harness.Delay.Delays);
        Assert.AreEqual(1, harness.Portal.TotpCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RejectedCredentials_ThrowsStableFailureWithoutTotpAttempt()
    {
        // Arrange
        var harness = new AuthenticationHarness();
        harness.Portal.CredentialResult = PamCredentialSubmissionResult.Rejected;

        // Act
        PamAuthenticationException exception = await Assert.ThrowsExactlyAsync<PamAuthenticationException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamAuthenticationErrorCode.CredentialsRejected, exception.ErrorCode);
        Assert.AreEqual(0, harness.Portal.TotpCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_UnknownCredentialResult_ThrowsUnexpectedResponseWithoutTotpAttempt()
    {
        // Arrange
        var harness = new AuthenticationHarness();
        harness.Portal.CredentialResult = (PamCredentialSubmissionResult)99;

        // Act
        PamAuthenticationException exception = await Assert.ThrowsExactlyAsync<PamAuthenticationException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamAuthenticationErrorCode.UnexpectedPortalResponse, exception.ErrorCode);
        Assert.AreEqual(0, harness.Portal.TotpCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_FirstTotpRejected_WaitsForRejectionAndRetriesOnce()
    {
        // Arrange
        var harness = new AuthenticationHarness(DateTimeOffset.UnixEpoch.AddSeconds(22));
        harness.Portal.TotpResults.Enqueue(PamTotpSubmissionResult.Rejected);
        harness.Portal.TotpResults.Enqueue(PamTotpSubmissionResult.Accepted);

        // Act
        await harness.Service.AuthenticateAsync(CreateCredentials());

        // Assert
        Assert.AreEqual(2, harness.Portal.TotpCalls);
        Assert.AreEqual(1, harness.Portal.RejectionDisappearanceCalls);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(8) }, harness.Delay.Delays);
    }

    [TestMethod]
    public async Task AuthenticateAsync_TwoTotpRejections_ThrowsStableFailureAfterSecondAttempt()
    {
        // Arrange
        var harness = new AuthenticationHarness(DateTimeOffset.UnixEpoch.AddSeconds(22));
        harness.Portal.TotpResults.Enqueue(PamTotpSubmissionResult.Rejected);
        harness.Portal.TotpResults.Enqueue(PamTotpSubmissionResult.Rejected);

        // Act
        PamAuthenticationException exception = await Assert.ThrowsExactlyAsync<PamAuthenticationException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamAuthenticationErrorCode.TotpRejected, exception.ErrorCode);
        Assert.AreEqual(2, harness.Portal.TotpCalls);
        Assert.AreEqual(0, harness.Portal.DashboardCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_UnknownTotpResult_ThrowsUnexpectedResponseWithoutRetry()
    {
        // Arrange
        var harness = new AuthenticationHarness();
        harness.Portal.TotpResults.Enqueue((PamTotpSubmissionResult)99);

        // Act
        PamAuthenticationException exception = await Assert.ThrowsExactlyAsync<PamAuthenticationException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamAuthenticationErrorCode.UnexpectedPortalResponse, exception.ErrorCode);
        Assert.AreEqual(1, harness.Portal.TotpCalls);
        Assert.AreEqual(0, harness.Portal.RejectionDisappearanceCalls);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("invalid")]
    public async Task AuthenticateAsync_MissingOrInvalidDate_PropagatesStableClockFailure(string? header)
    {
        // Arrange
        var harness = new AuthenticationHarness();
        harness.Session.Response = harness.Session.Response with { HttpDateHeader = header };
        harness.ClockFactory.Exception = new PamClockException(
            PamClockErrorCode.HttpDateUnavailableOrInvalid,
            "El portal PAM no proporcionó una hora HTTP válida.");

        // Act
        PamClockException exception = await Assert.ThrowsExactlyAsync<PamClockException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamClockErrorCode.HttpDateUnavailableOrInvalid, exception.ErrorCode);
        Assert.AreEqual(0, harness.Portal.CredentialCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ConnectionFailure_DoesNotRetry()
    {
        // Arrange
        var harness = new AuthenticationHarness();
        harness.Portal.TotpException = new InvalidOperationException("Synthetic connection failure.");

        // Act
        PamAuthenticationException exception = await Assert.ThrowsExactlyAsync<PamAuthenticationException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamAuthenticationErrorCode.ConnectionOrCertificateFailure, exception.ErrorCode);
        Assert.AreEqual(1, harness.Portal.TotpCalls);
        Assert.AreEqual(0, harness.Portal.RejectionDisappearanceCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_DashboardMissing_DoesNotRetry()
    {
        // Arrange
        var harness = new AuthenticationHarness();
        harness.Portal.DashboardConfirmed = false;

        // Act
        PamAuthenticationException exception = await Assert.ThrowsExactlyAsync<PamAuthenticationException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials()));

        // Assert
        Assert.AreEqual(PamAuthenticationErrorCode.ConfirmationMissing, exception.ErrorCode);
        Assert.AreEqual(1, harness.Portal.TotpCalls);
    }

    [TestMethod]
    public async Task AuthenticateAsync_CancelledBeforeStart_StopsWithoutPortalOperations()
    {
        // Arrange
        var harness = new AuthenticationHarness();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        // Act
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => harness.Service.AuthenticateAsync(CreateCredentials(), cancellationSource.Token));

        // Assert
        Assert.AreEqual(0, harness.BrowserFactory.CreateCalls);
        Assert.AreEqual(0, harness.Portal.CredentialCalls);
    }

    private static PamCredentials CreateCredentials() => new("test-user", "test-password", Seed);

    private sealed class AuthenticationHarness
    {
        public AuthenticationHarness(DateTimeOffset? now = null)
        {
            Clock = new ControlledClock(now ?? DateTimeOffset.UnixEpoch);
            Delay = new AdvancingDelay(Clock);
            Session = new FakeBrowserSession();
            BrowserFactory = new FakeBrowserSessionFactory(Session);
            Portal = new FakePortal();
            PortalFactory = new FakePortalFactory(Portal);
            ClockFactory = new FakeClockFactory(Clock);
            Service = new PamAuthenticationService(BrowserFactory, PortalFactory, ClockFactory, new TotpGenerator(), Delay);
        }

        public ControlledClock Clock { get; }
        public AdvancingDelay Delay { get; }
        public FakeBrowserSession Session { get; }
        public FakeBrowserSessionFactory BrowserFactory { get; }
        public FakePortal Portal { get; }
        public FakePortalFactory PortalFactory { get; }
        public FakeClockFactory ClockFactory { get; }
        public PamAuthenticationService Service { get; }
    }

    private sealed class ControlledClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
    }

    private sealed class AdvancingDelay(ControlledClock clock) : IAsyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            clock.Advance(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClockFactory(IClock clock) : IPamClockFactory
    {
        public Exception? Exception { get; set; }
        public string? ReceivedHttpDateHeader { get; private set; }
        public TimeSpan? ReceivedRoundTripTime { get; private set; }
        public IClock Create(string? httpDateHeader, TimeSpan roundTripTime)
        {
            ReceivedHttpDateHeader = httpDateHeader;
            ReceivedRoundTripTime = roundTripTime;
            return Exception is null ? clock : throw Exception;
        }
    }

    private sealed class FakeBrowserSessionFactory(IPamBrowserSession session) : IPamBrowserSessionFactory
    {
        public int CreateCalls { get; private set; }
        public Task<IPamBrowserSession> CreateAsync(CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(session);
        }
    }

    private sealed class FakeBrowserSession : IPamBrowserSession
    {
        public PamNavigationResponse Response { get; set; } = new(
            PamAuthenticationService.AuthorizedOrigin, 200, "Wed, 21 Oct 2015 07:28:00 GMT", TimeSpan.FromMilliseconds(240));
        public Uri? NavigatedUri { get; private set; }
        public Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            NavigatedUri = uri;
            return Task.FromResult(Response);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakePortalFactory(IPamAuthenticationPortal portal) : IPamAuthenticationPortalFactory
    {
        public IPamBrowserSession? ReceivedSession { get; private set; }
        public IPamAuthenticationPortal Create(IPamBrowserSession session)
        {
            ReceivedSession = session;
            return portal;
        }
    }

    private sealed class FakePortal : IPamAuthenticationPortal
    {
        public PamCredentialSubmissionResult CredentialResult { get; set; } = PamCredentialSubmissionResult.Accepted;
        public Queue<PamTotpSubmissionResult> TotpResults { get; } = [];
        public Exception? TotpException { get; set; }
        public bool DashboardConfirmed { get; set; } = true;
        public int CredentialCalls { get; private set; }
        public int TotpCalls { get; private set; }
        public int RejectionDisappearanceCalls { get; private set; }
        public int DashboardCalls { get; private set; }
        public Task<PamCredentialSubmissionResult> SubmitCredentialsAsync(PamCredentials credentials, CancellationToken cancellationToken)
        {
            CredentialCalls++;
            return Task.FromResult(CredentialResult);
        }
        public Task<PamTotpSubmissionResult> SubmitTotpAsync(TotpCode code, CancellationToken cancellationToken)
        {
            TotpCalls++;
            if (TotpException is not null) throw TotpException;
            return Task.FromResult(TotpResults.Count == 0 ? PamTotpSubmissionResult.Accepted : TotpResults.Dequeue());
        }
        public Task WaitForTotpRejectionToDisappearAsync(CancellationToken cancellationToken)
        {
            RejectionDisappearanceCalls++;
            return Task.CompletedTask;
        }
        public Task<bool> WaitForDashboardAsync(CancellationToken cancellationToken)
        {
            DashboardCalls++;
            return Task.FromResult(DashboardConfirmed);
        }
    }
}
