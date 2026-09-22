using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Browser;
using PamAutomation.Core.Diagnostics;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Validation;
using PamAutomation.Infrastructure.Diagnostics;

namespace PamAutomation.Tests.Core.Execution;

[TestClass]
[TestCategory("Unit")]
public sealed class PamAutomationExecutionServicePendingTests
{
    private static readonly PamCredentials Credentials = new("fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP");
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly AppSettings Settings = new()
    {
        Request = new RequestSettings
        {
            StartDate = new DateOnly(2026, 9, 22),
            StartTime = new TimeOnly(9, 0),
            SessionHours = 1,
            RequestReason = "private-fixture-reason",
        },
    };

    [TestMethod]
    public async Task PrepareAndExecute_RecoveryInBothStages_SubmitsOnceAndKeepsRecoveredBaseline()
    {
        var ports = new RecordingPorts(
            new TimeoutException(), new TimeoutException(), Rows("700001"),
            new TimeoutException(), new TimeoutException(), Rows("700001", "700002"));
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        PamAutomationExecutionService service = CreateService(ports, store, logs);

        PamExecutionPreparation preparation = await service.PrepareRealAsync(Credentials, Settings, Now);

        Assert.AreEqual(0, ports.SubmitCalls);
        Assert.AreEqual(0, ports.SubmissionFactoryCalls);
        Assert.AreEqual("700001", Assert.ContainsSingle(preparation.PendingRequestIdsBeforeSubmission));
        PendingReconciliationDiagnostic preparedTrace = store.CaptureSnapshot()!;
        Assert.AreEqual("Preflight", preparedTrace.Stage);
        Assert.AreEqual(1, preparedTrace.ExactPendingDuplicateCount);
        Assert.AreEqual("AwaitingRepeatedPendingDecision", preparedTrace.Decision);
        Assert.IsNotNull(preparedTrace.BeforeSubmission);
        Assert.AreEqual("700001", Assert.ContainsSingle(preparedTrace.BeforeSubmission.Requests).RequestId);

        PamExecutionResult result = await service.ExecuteRealAsync(Credentials, preparation, Now);

        Assert.AreEqual(1, ports.SubmitCalls);
        Assert.AreEqual(1, ports.SubmissionFactoryCalls);
        Assert.AreEqual("700002", Assert.ContainsSingle(result.RequestIds));
        Assert.AreEqual(6, ports.ReadCalls);
        Assert.AreEqual(6, ports.Sessions.Count); // Favorites, 2 preflight, submission, 2 reconciliation.
        Assert.IsTrue(ports.Sessions.All(session => session.DisposeCalls == 1));
        PendingReconciliationDiagnostic trace = store.CaptureSnapshot()!;
        Assert.AreEqual(preparedTrace.ExecutionId, trace.ExecutionId);
        Assert.AreEqual("Reconciliation", trace.Stage);
        Assert.AreEqual("SubmitAgain", trace.Decision);
        Assert.AreEqual(1, trace.SubmittedCount);
        Assert.IsNull(trace.FailureCode);
        Assert.HasCount(6, trace.Reads);
        Assert.HasCount(6, trace.Recoveries);
        Assert.IsNotNull(trace.BeforeSubmission);
        Assert.AreEqual(preparedTrace.BeforeSubmission.CapturedAtUtc, trace.BeforeSubmission.CapturedAtUtc);
        CollectionAssert.AreEqual(preparedTrace.BeforeSubmission.Requests.ToArray(), trace.BeforeSubmission.Requests.ToArray());
        Assert.AreEqual("700001", Assert.ContainsSingle(trace.BeforeSubmission.Requests).RequestId);
        Assert.AreEqual(1, Assert.ContainsSingle(trace.Attempts).BaselineIntersectionCount);
        Assert.AreEqual(1, Assert.ContainsSingle(trace.Attempts).NewRequestCount);

        string normalLog = JsonSerializer.Serialize(logs.CaptureSnapshot());
        foreach (string privateValue in new[]
        {
            "700001", "700002", "private-fixture-system", "private-fixture-account", "private-fixture-reason",
            "fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP", "123456",
        })
        {
            Assert.DoesNotContain(privateValue, normalLog);
        }
    }

    [TestMethod]
    public async Task PrepareRealAsync_PersistentTimeout_ExportsPreflightTraceWithoutBaselineOrSubmission()
    {
        var ports = new RecordingPorts(new TimeoutException(
            "fixture-user fixture-password JBSWY3DPEHPK3PXP 123456 private-fixture-account 700001"));
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        PamAutomationExecutionService service = CreateService(ports, store, logs);

        await Assert.ThrowsExactlyAsync<PamPendingException>(
            () => service.PrepareRealAsync(Credentials, Settings, Now));

        Assert.AreEqual(0, ports.SubmitCalls);
        Assert.AreEqual(0, ports.SubmissionFactoryCalls);
        Assert.AreEqual(4, ports.ReadCalls);
        PendingReconciliationDiagnostic trace = store.CaptureSnapshot()!;
        Assert.AreEqual(3, trace.SchemaVersion);
        Assert.AreEqual("Preflight", trace.Stage);
        Assert.IsNull(trace.BeforeSubmission);
        Assert.HasCount(1, trace.ExpectedRequests);
        Assert.HasCount(4, trace.Reads);
        Assert.HasCount(5, trace.Recoveries);
        Assert.AreEqual("PortalUnavailable", trace.FailureCode);
        Assert.IsEmpty(trace.Attempts);
        Assert.AreEqual(0, trace.SubmittedCount);

        string directory = Path.Combine(Path.GetTempPath(), "PamAutomation.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string destination = Path.Combine(directory, "preflight.zip");
            await new DiagnosticExportService(logs, pendingDiagnosticSource: store).ExportAsync(
                destination, new DiagnosticExportContext(true, true, false, null));
            using ZipArchive archive = ZipFile.OpenRead(destination);
            ZipArchiveEntry detail = archive.GetEntry("pending-reconciliation.json")!;
            Assert.IsNotNull(detail);
            using var reader = new StreamReader(detail.Open());
            using JsonDocument json = JsonDocument.Parse(await reader.ReadToEndAsync());
            Assert.AreEqual("Preflight", json.RootElement.GetProperty("stage").GetString());
            // The exporter deliberately omits null properties, including an unread baseline.
            Assert.IsFalse(json.RootElement.TryGetProperty("beforeSubmission", out _));
            Assert.AreEqual(4, json.RootElement.GetProperty("reads").GetArrayLength());
            Assert.AreEqual("PortalUnavailable", json.RootElement.GetProperty("failureCode").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        string normalLog = JsonSerializer.Serialize(logs.CaptureSnapshot());
        foreach (string privateValue in new[] { "fixture-user", "fixture-password", "JBSWY3DPEHPK3PXP", "123456", "private-fixture-account", "700001" })
        {
            Assert.DoesNotContain(privateValue, normalLog);
        }
    }

    private static PamAutomationExecutionService CreateService(
        RecordingPorts ports,
        InMemoryPendingReconciliationDiagnosticStore store,
        RuntimeDiagnosticLoggerFactory logs)
    {
        var validator = new RequestSettingsValidator();
        var favorites = new PamFavoritesService(ports, ports);
        return new PamAutomationExecutionService(
            favorites,
            new PamRequestSubmissionService(ports, favorites, ports, validator),
            new PamPendingService(ports, ports, ports, store, logger: logs.CreateLogger<PamPendingService>()),
            new PamRequestTestService(ports, favorites, ports, validator),
            new AppSettingsValidator(validator),
            logger: logs.CreateLogger<PamAutomationExecutionService>(),
            pendingDiagnosticSink: store);
    }

    private static IReadOnlyList<PamPendingRequest> Rows(params string[] ids) => ids.Select(id =>
        new PamPendingRequest(id, "private-fixture-system", "arbitrary-label", "private-fixture-account",
            new DateOnly(2026, 9, 22), new TimeOnly(9, 0), new DateOnly(2026, 9, 22), new TimeOnly(10, 0),
            "Pending", "private-fixture-reason")).ToArray();

    private sealed class RecordingPorts(params object[] outcomes) :
        IPamAuthenticatedSessionFactory, IPamFavoritesPortalFactory, IPamFavoritesPortal,
        IPamPendingPortalFactory, IPamPendingPortal, IPamRequestSubmissionPortalFactory,
        IPamRequestSubmissionPortal, IPamRequestTestPortalFactory, IAsyncDelay
    {
        public List<Session> Sessions { get; } = [];
        public int ReadCalls { get; private set; }
        public int SubmitCalls { get; private set; }
        public int SubmissionFactoryCalls { get; private set; }

        public Task<IPamBrowserSession> CreateAuthenticatedAsync(PamCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.IsTrue(Sessions.All(session => session.DisposeCalls == 1));
            var session = new Session();
            Sessions.Add(session);
            return Task.FromResult<IPamBrowserSession>(session);
        }

        IPamFavoritesPortal IPamFavoritesPortalFactory.Create(IPamBrowserSession session) => this;
        IPamPendingPortal IPamPendingPortalFactory.Create(IPamBrowserSession session) => this;
        IPamRequestTestPortal IPamRequestTestPortalFactory.Create(IPamBrowserSession session) =>
            throw new NotSupportedException();

        IPamRequestSubmissionPortal IPamRequestSubmissionPortalFactory.Create(IPamBrowserSession session)
        {
            SubmissionFactoryCalls++;
            return this;
        }

        Task<IReadOnlyList<PamFavoriteAccount>> IPamFavoritesPortal.ReadAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PamFavoriteAccount>>(
                [new("private-fixture-system", "favorite-label", "private-fixture-account")]);

        Task<IReadOnlyList<PamPendingRequest>> IPamPendingPortal.ReadAllAsync(CancellationToken cancellationToken)
        {
            object outcome = outcomes[Math.Min(ReadCalls++, outcomes.Length - 1)];
            return outcome is Exception exception
                ? Task.FromException<IReadOnlyList<PamPendingRequest>>(exception)
                : Task.FromResult((IReadOnlyList<PamPendingRequest>)outcome);
        }

        public Task<PamRequestPortalOutcome> SubmitAsync(PamFavoriteAccount account, PamRequestFormData form,
            CancellationToken cancellationToken)
        {
            SubmitCalls++;
            return Task.FromResult(PamRequestPortalOutcome.Submitted());
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class Session : IPamBrowserSession
    {
        public int DisposeCalls { get; private set; }
        public Task<PamNavigationResponse> NavigateAsync(Uri uri, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
