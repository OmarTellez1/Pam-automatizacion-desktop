using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Interfaces.Authentication;
using PamAutomation.Core.Interfaces.Browser;
using PamAutomation.Core.Interfaces.Favorites;
using PamAutomation.Core.Interfaces.Outlook;
using PamAutomation.Core.Interfaces.Requests;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Models.Credentials;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Requests;
using PamAutomation.Core.Validation;

namespace PamAutomation.Tests.Core.Execution;

[TestClass]
[TestCategory("Unit")]
public sealed class PamAutomationExecutionServiceEmailTests
{
    [TestMethod]
    public async Task OrdinaryRepeatedEmail_RemainsBlockedByInMemoryGuard()
    {
        var outlook = new RecordingOutlookClient();
        PamAutomationExecutionService service = CreateService(outlook);
        MailSettings mail = CreateMailSettings();

        OutlookDeliveryResult first = await service.SendResultEmailAsync(mail, ["7001"], isTest: false);
        OutlookDeliveryResult repeated = await service.SendResultEmailAsync(mail, ["7001"], isTest: false);

        Assert.AreEqual(OutlookDeliveryStatus.SentConfirmed, first.Status);
        Assert.AreEqual(OutlookDeliveryStatus.DuplicateInMemory, repeated.Status);
        Assert.AreEqual(1, outlook.SendCount);
        Assert.AreEqual(2, outlook.LookupCount);
    }

    [TestMethod]
    public async Task ExistingPendingExplicitEmailChoice_BypassesBothDuplicateGuardsAndSendsAgain()
    {
        var outlook = new RecordingOutlookClient();
        PamAutomationExecutionService service = CreateService(outlook);
        MailSettings mail = CreateMailSettings();

        OutlookDeliveryResult first = await service.SendResultEmailAsync(mail, ["7001"], isTest: false);
        var preparation = new PamExecutionPreparation(
            favoriteCount: 1,
            request: new RequestSettings
            {
                StartDate = new DateOnly(2026, 9, 21),
                StartTime = new TimeOnly(18, 0),
                SessionHours = 1,
                RequestReason = "Pruebas",
            },
            finalAction: FinalAction.SendEmail,
            pendingRequestIdsBeforeSubmission: ["7001"],
            exactPendingDuplicateCount: 1,
            reusablePendingRequestIds: ["7001"]);

        PamExecutionResult repeated = await service.CompleteWithExistingPendingAsync(preparation, mail);

        Assert.AreEqual(OutlookDeliveryStatus.SentConfirmed, first.Status);
        Assert.AreEqual(OutlookDeliveryStatus.SentConfirmed, repeated.MailDelivery?.Status);
        Assert.AreEqual(2, outlook.SendCount);
        Assert.AreEqual(3, outlook.LookupCount);
    }

    private static PamAutomationExecutionService CreateService(IOutlookClassicClient outlook)
    {
        var unused = new UnusedPamDependencies();
        var requestValidator = new RequestSettingsValidator();
        var favorites = new PamFavoritesService(unused, unused);
        return new PamAutomationExecutionService(
            favorites,
            new PamRequestSubmissionService(unused, favorites, unused, requestValidator),
            new PamPendingService(unused, unused, unused),
            new PamRequestTestService(unused, favorites, unused, requestValidator),
            new AppSettingsValidator(requestValidator),
            outlookClient: outlook,
            outlookConfirmationDelay: unused);
    }

    [TestMethod]
    public async Task DuplicateLookupFailure_BlocksBeforeSend()
    {
        var outlook = new RecordingOutlookClient
        {
            DuplicateLookupFailure = new OutlookOperationException(
                OutlookOperationErrorCode.DuplicateCheckFailed, new InvalidOperationException("Unreadable message."))
        };
        var service = CreateService(outlook);

        await Assert.ThrowsExactlyAsync<OutlookOperationException>(() =>
            service.SendResultEmailAsync(CreateMailSettings(), ["7001"], isTest: false));

        Assert.AreEqual(0, outlook.SendCount);
    }

    [TestMethod]
    public async Task AcceptedSendWithObservationFailure_RemainsProtectedAgainstRepeatedSend()
    {
        var outlook = new RecordingOutlookClient
        {
            ObservationFailure = new OutlookOperationException(
                OutlookOperationErrorCode.DeliveryObservationFailed, new InvalidOperationException("Permanent failure."))
        };
        var service = CreateService(outlook);

        var first = await service.SendResultEmailAsync(CreateMailSettings(), ["7001"], isTest: false);
        var repeated = await service.SendResultEmailAsync(CreateMailSettings(), ["7001"], isTest: false);

        Assert.AreEqual(OutlookDeliveryStatus.SubmittedUnconfirmed, first.Status);
        Assert.AreEqual(OutlookDeliveryStatus.DuplicateInMemory, repeated.Status);
        Assert.AreEqual(1, outlook.SendCount);
    }

    private static MailSettings CreateMailSettings() => new()
    {
        SmtpAddress = "sender@example.test",
        ToRecipients = ["recipient@example.test"],
        Subject = "Resultados PAM",
        HtmlTemplate = "<p>{{REQUEST_IDS}}</p>",
    };

    private sealed class RecordingOutlookClient : IOutlookClassicClient
    {
        public int LookupCount { get; private set; }
        public int SendCount { get; private set; }
        public Exception? DuplicateLookupFailure { get; init; }
        public Exception? ObservationFailure { get; init; }

        public Task<OutlookExistingMessageStatus> ObserveDeliveryAsync(
            string smtpAddress, string subject, IReadOnlyList<string> requestIds, bool includeUnsentFolders,
            CancellationToken cancellationToken = default) =>
            ObservationFailure is null
                ? FindExistingAsync(smtpAddress, subject, requestIds, cancellationToken)
                : Task.FromException<OutlookExistingMessageStatus>(ObservationFailure);

        public Task<OutlookExistingMessageStatus> FindExistingAsync(
            string smtpAddress,
            string subject,
            IReadOnlyList<string> requestIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCount++;
            if (DuplicateLookupFailure is not null) throw DuplicateLookupFailure;
            return Task.FromResult(
                SendCount == 0 ? OutlookExistingMessageStatus.None : OutlookExistingMessageStatus.Sent);
        }

        public Task<OutlookAccount> ResolveAccountAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OutlookAccount(smtpAddress));

        public Task ValidateAccountAsync(
            string smtpAddress,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<OutlookDeliveryResult> SendAsync(
            OutlookSendRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCount++;
            return Task.FromResult(OutlookDeliveryResult.SubmittedUnconfirmed());
        }
    }

    private sealed class UnusedPamDependencies :
        IPamAuthenticatedSessionFactory,
        IPamFavoritesPortalFactory,
        IPamRequestSubmissionPortalFactory,
        IPamPendingPortalFactory,
        IPamRequestTestPortalFactory,
        IAsyncDelay
    {
        public Task<IPamBrowserSession> CreateAuthenticatedAsync(
            PamCredentials credentials,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        IPamFavoritesPortal IPamFavoritesPortalFactory.Create(IPamBrowserSession session) =>
            throw new NotSupportedException();

        IPamRequestSubmissionPortal IPamRequestSubmissionPortalFactory.Create(IPamBrowserSession session) =>
            throw new NotSupportedException();

        IPamPendingPortal IPamPendingPortalFactory.Create(IPamBrowserSession session) =>
            throw new NotSupportedException();

        IPamRequestTestPortal IPamRequestTestPortalFactory.Create(IPamBrowserSession session) =>
            throw new NotSupportedException();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
