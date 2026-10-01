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
            new TimeoutException(), new TimeoutException(),
            (IReadOnlyList<PamPendingRequest>)[UnrelatedRow("700001")],
            new TimeoutException(), new TimeoutException(),
            (IReadOnlyList<PamPendingRequest>)[UnrelatedRow("700001"), ExactRow("700002")]);
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        PamAutomationExecutionService service = CreateService(ports, store, logs);

        PamExecutionPreparation preparation = await service.PrepareRealAsync(Credentials, Settings, Now);

        Assert.AreEqual(0, ports.SubmitCalls);
        Assert.AreEqual(0, ports.SubmissionFactoryCalls);
        Assert.AreEqual("700001", Assert.ContainsSingle(preparation.PendingRequestIdsBeforeSubmission));
        PendingReconciliationDiagnostic preparedTrace = store.CaptureSnapshot()!;
        Assert.AreEqual("Preflight", preparedTrace.Stage);
        Assert.AreEqual(0, preparedTrace.ExactPendingDuplicateCount);
        Assert.AreEqual("Submit", preparedTrace.Decision);
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
        Assert.AreEqual("Submit", trace.Decision);
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
    public async Task ExecuteRealAsync_ExactPendingDuplicateExists_BlocksBeforeSubmission()
    {
        var ports = new RecordingPorts(Rows("700001"));
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        PamAutomationExecutionService service = CreateService(ports, store, logs);
        PamExecutionPreparation preparation = await service.PrepareRealAsync(Credentials, Settings, Now);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.ExecuteRealAsync(Credentials, preparation, Now));

        Assert.Contains("No se permite crear solicitudes exactamente iguales", exception.Message);
        Assert.AreEqual(1, preparation.ExactPendingDuplicateCount);
        Assert.AreEqual(0, ports.SubmitCalls);
        Assert.AreEqual(0, ports.SubmissionFactoryCalls);
        Assert.AreEqual("BlockedExistingPending", store.CaptureSnapshot()!.Decision);
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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteRealAsync_LostResponse_RecoversOnlyNewIdsWithoutResubmitting(bool appears)
    {
        var ports = new RecordingPorts((IReadOnlyList<PamPendingRequest>)[], appears ? Rows("700002") : (IReadOnlyList<PamPendingRequest>)[]);
        ports.SubmissionOutcomes = [new TimeoutException("lost response")];
        var mail = new RecordingMail();
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var service = CreateService(ports, store, logs, mail);
        var preparation = await service.PrepareRealAsync(Credentials, Settings with { FinalAction = FinalAction.SendEmail }, Now);
        var mailSettings = new MailSettings { SmtpAddress = "sender@example.test", ToRecipients = ["recipient@example.test"], Subject = "Fixture", HtmlTemplate = "<p>{{REQUEST_IDS}}</p>" };
        var failure = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() => service.ExecuteRealAsync(Credentials, preparation, Now, mailSettings));
        Assert.IsNotNull(failure.PartialRun);
        Assert.AreEqual(1, failure.PartialRun.UncertainCount);
        Assert.AreEqual(appears ? 0 : 1, failure.PartialRun.UnresolvedCount);
        Assert.AreEqual(appears ? 1 : 0, failure.PartialRun.PendingVerifiedCount);
        Assert.AreEqual(appears ? 1 : 0, failure.RecoveredRequestIds.Count);
        Assert.AreEqual(1, ports.SubmitCalls);
        Assert.IsFalse(failure.PartialRun.IsComplete);
        Assert.AreEqual(0, mail.Calls);
    }

    [TestMethod]
    public async Task ExecuteRealAsync_TwoAcceptancesThenCalendarFailure_PreservesKnownRowsAndStops()
    {
        var ports = new RecordingPorts((IReadOnlyList<PamPendingRequest>)[], Rows("700002", "700003"))
        {
            Accounts = [
                new("private-fixture-system", "application-1", "private-fixture-account"),
                new("private-fixture-system", "application-2", "private-fixture-account"),
                new("other-system", "application-3", "other-account"),
                new("last-system", "application-4", "last-account")],
            SubmissionOutcomes = [PamRequestPortalOutcome.Submitted(), PamRequestPortalOutcome.Submitted(),
                new PamCalendarSelectionException(PamCalendarFailure.MonthUnreadable, new(2026, 9, 22), null, 0)]
        };
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var service = CreateService(ports, store, logs);
        var preparation = await service.PrepareRealAsync(Credentials, Settings, Now);
        var failure = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() => service.ExecuteRealAsync(Credentials, preparation, Now));
        Assert.IsNotNull(failure.PartialRun);
        Assert.AreEqual(2, failure.PartialRun.SubmittedCount);
        Assert.AreEqual(1, failure.PartialRun.FailedBeforeSubmissionCount);
        Assert.AreEqual(1, failure.PartialRun.NotAttemptedCount);
        Assert.HasCount(2, failure.RecoveredRequestIds);
        Assert.AreEqual(3, ports.SubmitCalls); // Portal entries; third fails before a click.
        Assert.AreEqual(2, store.CaptureSnapshot()!.SubmittedCount);
        Assert.IsNotNull(store.CaptureSnapshot()!.SubmissionCounts);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public async Task ExecuteRealAsync_IndistinguishableGroupShortOrExcess_DoesNotAssignIdsByOrder(int observed)
    {
        var ports = new RecordingPorts((IReadOnlyList<PamPendingRequest>)[], Rows(Enumerable.Range(0, observed).Select(i => (700002 + i).ToString()).ToArray()))
        {
            Accounts = [new("private-fixture-system", "application-1", "private-fixture-account"),
                new("PRIVATE-FIXTURE-SYSTEM", "application-2", "private-fixture-account")],
            SubmissionOutcomes = [PamRequestPortalOutcome.Submitted(), new TimeoutException()]
        };
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var service = CreateService(ports, store, logs);
        var preparation = await service.PrepareRealAsync(Credentials, Settings, Now);
        var failure = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() => service.ExecuteRealAsync(Credentials, preparation, Now));
        Assert.IsEmpty(failure.RecoveredRequestIds);
        Assert.AreEqual(2, ports.SubmitCalls);
        Assert.AreEqual(1, failure.PartialRun!.SubmittedCount);
        Assert.AreEqual(1, failure.PartialRun.UncertainCount);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task ExecuteRealAsync_CanceledAfterAcceptance_PreservesAcceptanceAndDoesNotReadOrSendAgain(int planned)
    {
        using var cancel = new CancellationTokenSource();
        var ports = new RecordingPorts((IReadOnlyList<PamPendingRequest>)[])
        {
            Accounts = new PamFavoriteAccount[] { new("private-fixture-system", "application-1", "private-fixture-account"), new("other", "application-2", "other") }.Take(planned).ToArray(),
            AfterSubmit = () => cancel.Cancel()
        };
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var service = CreateService(ports, store, logs);
        var preparation = await service.PrepareRealAsync(Credentials, Settings, Now);
        var failure = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
            service.ExecuteRealAsync(Credentials, preparation, Now, cancellationToken: cancel.Token));
        Assert.AreEqual(PamRequestSubmissionErrorCode.Canceled, failure.ErrorCode);
        Assert.AreEqual(1, failure.PartialRun!.SubmittedCount);
        Assert.AreEqual(planned - 1, failure.PartialRun.NotAttemptedCount);
        Assert.AreEqual(1, ports.SubmitCalls);
        Assert.AreEqual(1, ports.ReadCalls); // Only the original preflight.
    }


    [TestMethod]
    public async Task ExecuteRealAsync_PendingReadFailsAfterPartialMatch_KeepsRecoveredIdAndUnresolvedRow()
    {
        var ports = new RecordingPorts((IReadOnlyList<PamPendingRequest>)[], Rows("700002"),
            new PamPendingException(PamPendingErrorCode.PortalUnavailable))
        {
            Accounts = [new("private-fixture-system", "application-1", "private-fixture-account"),
                new("other-system", "application-2", "other-account")],
            SubmissionOutcomes = [PamRequestPortalOutcome.Submitted(), new TimeoutException()]
        };
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var service = CreateService(ports, store, logs);
        var preparation = await service.PrepareRealAsync(Credentials, Settings, Now);

        var failure = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() =>
            service.ExecuteRealAsync(Credentials, preparation, Now));

        Assert.AreEqual(PamRequestSubmissionErrorCode.ReconciliationFailedAfterSubmission, failure.ErrorCode);
        Assert.IsNotNull(failure.PartialRun);
        Assert.HasCount(1, failure.RecoveredRequestIds);
        Assert.AreEqual("700002", failure.RecoveredRequestIds[0]);
        Assert.AreEqual(1, failure.PartialRun.SubmittedCount);
        Assert.AreEqual(1, failure.PartialRun.PendingVerifiedCount);
        Assert.AreEqual(1, failure.PartialRun.UnresolvedCount);
        Assert.AreEqual(2, ports.SubmitCalls);
        Assert.IsGreaterThan(2, ports.ReadCalls);
        Assert.AreEqual(1, store.CaptureSnapshot()!.SubmissionCounts!.RequestIdCount);
    }

    private static PamAutomationExecutionService CreateService(
        RecordingPorts ports,
        InMemoryPendingReconciliationDiagnosticStore store,
        RuntimeDiagnosticLoggerFactory logs,
        PamAutomation.Core.Interfaces.Outlook.IOutlookClassicClient? mail = null)
    {
        var validator = new RequestSettingsValidator();
        var favorites = new PamFavoritesService(ports, ports);
        return new PamAutomationExecutionService(
            favorites,
            new PamRequestSubmissionService(ports, favorites, ports, validator, store),
            new PamPendingService(ports, ports, ports, store, logger: logs.CreateLogger<PamPendingService>()),
            new PamRequestTestService(ports, favorites, ports, validator),
            new AppSettingsValidator(validator),
            outlookClient: mail,
            logger: logs.CreateLogger<PamAutomationExecutionService>(),
            pendingDiagnosticSink: store);
    }

    private static IReadOnlyList<PamPendingRequest> Rows(params string[] ids) =>
        ids.Select(ExactRow).ToArray();

    private static PamPendingRequest ExactRow(string id) =>
        new(id, "private-fixture-system", "arbitrary-label", "private-fixture-account",
            new DateOnly(2026, 9, 22), new TimeOnly(9, 0), new DateOnly(2026, 9, 22), new TimeOnly(10, 0),
            "Pending", "private-fixture-reason");

    private static PamPendingRequest UnrelatedRow(string id) =>
        new(id, "unrelated-system", "unrelated-application", "unrelated-account",
            new DateOnly(2026, 9, 22), new TimeOnly(9, 0), new DateOnly(2026, 9, 22), new TimeOnly(10, 0),
            "Pending", "private-fixture-reason");

    private sealed class RecordingPorts(params object[] outcomes) :
        IPamAuthenticatedSessionFactory, IPamFavoritesPortalFactory, IPamFavoritesPortal,
        IPamPendingPortalFactory, IPamPendingPortal, IPamRequestSubmissionPortalFactory,
        IPamRequestSubmissionPortal, IPamRequestTestPortalFactory, IAsyncDelay
    {
        public List<Session> Sessions { get; } = [];
        public int ReadCalls { get; private set; }
        public int SubmitCalls { get; private set; }
        public int SubmissionFactoryCalls { get; private set; }
        public IReadOnlyList<PamFavoriteAccount> Accounts { get; init; } = [new("private-fixture-system", "favorite-label", "private-fixture-account")];
        public object[] SubmissionOutcomes { get; set; } = [];
        public Action? AfterSubmit { get; init; }

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
            Task.FromResult(Accounts);

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
            object outcome = SubmissionOutcomes.Length > 0 ? SubmissionOutcomes[SubmitCalls - 1] : PamRequestPortalOutcome.Submitted();
            AfterSubmit?.Invoke();
            return outcome is Exception exception ? Task.FromException<PamRequestPortalOutcome>(exception) : Task.FromResult((PamRequestPortalOutcome)outcome);
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMail : PamAutomation.Core.Interfaces.Outlook.IOutlookClassicClient
    {
        public int Calls;
        public Task<PamAutomation.Core.Outlook.OutlookAccount> ResolveAccountAsync(string address, CancellationToken token = default) => throw new NotSupportedException();
        public Task ValidateAccountAsync(string address, CancellationToken token = default) => throw new NotSupportedException();
        public Task<PamAutomation.Core.Outlook.OutlookExistingMessageStatus> FindExistingAsync(string address, string subject, IReadOnlyList<string> ids, CancellationToken token = default) { Calls++; throw new NotSupportedException(); }
        public Task<PamAutomation.Core.Outlook.OutlookExistingMessageStatus> ObserveDeliveryAsync(string address, string subject, IReadOnlyList<string> ids, bool unsent, CancellationToken token = default) { Calls++; throw new NotSupportedException(); }
        public Task<PamAutomation.Core.Outlook.OutlookDeliveryResult> SendAsync(PamAutomation.Core.Outlook.OutlookSendRequest request, CancellationToken token = default) { Calls++; throw new NotSupportedException(); }
    }

    [TestMethod]
    public async Task ExecuteRealAsync_ConflictThenExplicitRejection_PreservesBothAndStops()
    {
        var ports = new RecordingPorts((IReadOnlyList<PamPendingRequest>)[])
        {
            Accounts = [new("one", "app-1", "one"), new("two", "app-2", "two"), new("three", "app-3", "three")],
            SubmissionOutcomes = [PamRequestPortalOutcome.ApprovedAccessOverlap(),
                new PamRequestSubmissionException(PamRequestSubmissionErrorCode.RequestRejected, submitMayHaveOccurred: false, isKnownRejection: true)]
        };
        using var logs = new RuntimeDiagnosticLoggerFactory();
        var store = new InMemoryPendingReconciliationDiagnosticStore();
        var service = CreateService(ports, store, logs);
        var preparation = await service.PrepareRealAsync(Credentials, Settings, Now);
        var failure = await Assert.ThrowsExactlyAsync<PamRequestSubmissionException>(() => service.ExecuteRealAsync(Credentials, preparation, Now));
        Assert.AreEqual(1, failure.PartialRun!.ConflictCount);
        Assert.AreEqual(1, failure.PartialRun.RejectedCount);
        Assert.AreEqual(1, failure.PartialRun.NotAttemptedCount);
        Assert.AreEqual(0, failure.PartialRun.UncertainCount);
        Assert.AreEqual(2, ports.SubmitCalls);
        Assert.AreEqual(1, ports.ReadCalls);
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
