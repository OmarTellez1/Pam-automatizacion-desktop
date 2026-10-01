using Microsoft.VisualStudio.TestTools.UnitTesting;
using PamAutomation.App.ViewModels;
using PamAutomation.Core.Execution;
using PamAutomation.Core.Favorites;
using PamAutomation.Core.Models.Configuration;
using PamAutomation.Core.Outlook;
using PamAutomation.Core.Validation;
using PamAutomation.Tests.Core.Execution;

namespace PamAutomation.Tests;

public sealed partial class MainWindowViewModelTests
{
    private static async Task<(MainWindowViewModel Vm, RequestIdAccumulator Accumulator,
        FakeExecutionService Execution, FakeDialogs Dialogs, FakeFavoriteCollectionService Collections)>
        CreateAccumulationAsync(bool mailConfigured = true)
    {
        var now = new DateTimeOffset(2026, 9, 10, 16, 0, 0, TimeSpan.FromHours(-5));
        FavoriteCollection collection = Core.Outlook.CollectionMailComposerTests.Collection("Operations", "operations");
        if (!mailConfigured) collection = new FavoriteCollection(collection.Id, collection.Name, collection.RequestReason, collection.Accounts);
        var collections = new FakeFavoriteCollectionService(new FavoriteCollectionsState([collection], collection.Id));
        var accumulator = new RequestIdAccumulator();
        var execution = new FakeExecutionService(now);
        var dialogs = new FakeDialogs(true);
        var vm = new MainWindowViewModel(execution,
            new FakeSettingsStore(CreateSettingsForCollectionTest("Reason", new TimeOnly(17, 0)) with { FinalAction = FinalAction.SendEmail }),
            new FakeCredentialStore(), dialogs, new AppSettingsValidator(new RequestSettingsValidator()),
            new FixedTimeProvider(now.ToUniversalTime()), configurationDialogs: new FakeConfigurationDialogs(),
            mailSettingsStore: new FakeMailSettingsStore(), favoriteCollectionService: collections,
            mailSignatureStore: new FakeSignatureStore(), accumulator: accumulator);
        await vm.InitializeAsync();
        vm.ToggleAccumulationCommand.Execute(null);
        return (vm, accumulator, execution, dialogs, collections);
    }

    [TestMethod]
    public async Task Accumulation_PartialResultShowsRecoveredIdsAndRetainsLastSuccessAndPreviousAccumulator()
    {
        var (vm, accumulator, execution, _, _) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        var account = new PamAutomation.Core.Favorites.PamFavoriteAccount("fixture", "fixture", "fixture");
        var partial = new PamAutomation.Core.Requests.PamRequestRunResult([
            new(account, PamAutomation.Core.Requests.PamRequestSubmissionStatus.Submitted, null),
            new(account, PamAutomation.Core.Requests.PamRequestSubmissionStatus.FailedBeforeSubmission, null)]);
        execution.Failure = new PamAutomation.Core.Requests.PamRequestSubmissionException(
            PamAutomation.Core.Requests.PamRequestSubmissionErrorCode.PortalUnavailable, processedCount: 1,
            partialRun: partial, recoveredRequestIds: ["9001"]);
        await vm.StartAutomationCommand.ExecuteAsync(null);
        CollectionAssert.AreEqual(new[] { "8001", "8002" }, accumulator.RequestIds.ToArray());
        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual("Automatización incompleta", vm.ResultTitle);
        Assert.AreEqual("9001", vm.RequestIdsText);
        Assert.AreEqual(0, execution.EmailSendCount);
        var context = vm.CaptureDiagnosticContext();
        Assert.AreEqual(2, context.LastExecution!.RequestIdCount);
        Assert.AreEqual("Partial", context.LastAttempt!.Status);
        Assert.AreEqual(1, context.LastAttempt.AcceptedCount);
    }


    [TestMethod]
    public async Task Accumulation_RunsWithoutMail_EightPlusSix_NoAutomaticFinalAction_PreservesPreference()
    {
        var (vm, accumulator, execution, dialogs, _) = await CreateAccumulationAsync(false);
        Assert.IsTrue(vm.CanStartAutomation);
        Assert.IsFalse(vm.CanChangeFinalAction);
        Assert.IsFalse(vm.IsTestActionVisible);
        await vm.ToggleFinalActionCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.SendEmailAtEnd);
        execution.NextResult = RequestIdAccumulatorTests.Batch(Enumerable.Range(100, 8).Select(i => i.ToString()).ToArray());
        await vm.StartAutomationCommand.ExecuteAsync(null);
        execution.NextResult = RequestIdAccumulatorTests.Batch(Enumerable.Range(108, 6).Select(i => i.ToString()).ToArray());
        await vm.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(14, vm.AccumulatedCount);
        Assert.IsTrue(dialogs.LastConfirmationAccumulates);
        Assert.AreEqual(FinalAction.DisplayRequestIds, execution.LastPreparedSettings!.FinalAction);
        Assert.IsNull(execution.LastExecutionMail);
        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasRequestIds);
        Assert.AreEqual(0, dialogs.ResultDisplayCount);
        Assert.AreEqual(0, execution.EmailSendCount);
        vm.ToggleAccumulationCommand.Execute(null);
        Assert.IsTrue(vm.IsAccumulating);
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(14, accumulator.Count);
        Assert.AreEqual(0, execution.EmailSendCount);
        Assert.Contains("correo", dialogs.LastError!);
        vm.DiscardAccumulatedCommand.Execute(null);
        vm.ToggleAccumulationCommand.Execute(null);
        Assert.IsFalse(vm.IsAccumulating);
        Assert.IsTrue(vm.SendEmailAtEnd);
        Assert.IsTrue(vm.CanChangeFinalAction);
    }

    [TestMethod]
    public async Task Accumulation_FailedPartialOrDuplicateRun_PreservesPriorIdsWithoutReusingPending()
    {
        var (vm, accumulator, execution, dialogs, _) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        execution.Failure = new OperationCanceledException();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(2, accumulator.Count);
        Assert.Contains("Pueden existir solicitudes", dialogs.LastError!);
        execution.Failure = null;
        execution.NextResult = new PamExecutionResult(PamExecutionMode.Real, DateTimeOffset.UtcNow,
            FinalAction.DisplayRequestIds, 3, 3, 0, ["9001"]);
        await vm.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(2, accumulator.Count);
        execution.PendingDuplicateCount = 1;
        execution.ReusablePendingIds = ["7001"];
        await vm.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(0, dialogs.RepeatedPendingConfirmationCount);
        Assert.AreEqual(0, execution.ExistingPendingCompletionCount);
        Assert.AreEqual(0, execution.EmailSendCount);
        CollectionAssert.AreEqual(new[] { "8001", "8002" }, accumulator.RequestIds.ToArray());
    }

    [TestMethod]
    public async Task Accumulation_ManualSend_UsesCurrentCollectionMailAndCommonSignature_ThenUnlocks()
    {
        var (vm, accumulator, execution, dialogs, collections) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        FavoriteCollection original = collections.State.ActiveCollection!;
        var updated = new FavoriteCollection(original.Id, original.Name, original.RequestReason, original.Accounts,
            mail: original.Mail! with { Subject = "Updated subject" });
        collections.State = new FavoriteCollectionsState([updated], updated.Id);
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(1, execution.EmailSendCount);
        Assert.AreEqual(1, dialogs.AccumulatedConfirmationCount);
        MailSettings sent = execution.LastAccumulatedMail!;
        Assert.AreEqual("Updated subject", sent.Subject);
        Assert.AreEqual("sender@example.test", sent.SmtpAddress);
        Assert.AreEqual("operations@example.com", sent.ToRecipients[0]);
        Assert.AreEqual("operations.cc@example.com", sent.CcRecipients[0]);
        Assert.AreEqual(1, sent.HtmlTemplate.Split("Firma inicial").Length - 1);
        CollectionAssert.AreEqual(new[] { "8001", "8002" }, execution.LastEmailIds);
        Assert.AreEqual(0, accumulator.Count);
        Assert.IsNull(accumulator.CollectionId);
        Assert.IsTrue(vm.IsAccumulating);
        Assert.IsTrue(vm.CanToggleAccumulation);
    }

    [TestMethod]
    public async Task Accumulation_CancelledConfirmationAndSafeFailure_KeepIdsAndPermitRetry()
    {
        var (vm, accumulator, execution, dialogs, _) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        dialogs.ConfirmAccumulated = false;
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(0, execution.EmailSendCount);
        Assert.AreEqual(2, accumulator.Count);
        dialogs.ConfirmAccumulated = true;
        execution.AccumulatedFailure = new InvalidOperationException("Safe pre-send failure");
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(2, accumulator.Count);
        Assert.AreEqual(AccumulatedMailState.Ready, accumulator.MailState);
        execution.AccumulatedFailure = null;
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(0, accumulator.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Accumulation_UncertainSend_ObservesFrozenMailAndNeverResends(bool throws)
    {
        var (vm, accumulator, execution, dialogs, collections) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        execution.AccumulatedDelivery = OutlookDeliveryResult.OutboxPending();
        if (throws) execution.AccumulatedFailure = new OutlookDeliveryUncertainException(new InvalidOperationException());
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(AccumulatedMailState.AwaitingConfirmation, accumulator.MailState);
        Assert.IsFalse(vm.CanStartAutomation);
        Assert.AreEqual("Comprobar envío", vm.AccumulatedMailActionText);
        MailSettings frozen = execution.LastAccumulatedMail!;
        FavoriteCollection old = collections.State.ActiveCollection!;
        collections.State = new FavoriteCollectionsState([new FavoriteCollection(old.Id, old.Name, old.RequestReason, old.Accounts)], old.Id);
        execution.AccumulatedFailure = new InvalidOperationException("Observation failure");
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(2, accumulator.Count);
        Assert.AreEqual(AccumulatedMailState.AwaitingConfirmation, accumulator.MailState);
        execution.AccumulatedFailure = null;
        execution.AccumulatedDelivery = OutlookDeliveryResult.SentConfirmed();
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(1, execution.EmailSendCount);
        Assert.AreEqual(2, execution.ObservationCount);
        Assert.AreSame(frozen, execution.LastAccumulatedMail);
        Assert.AreEqual(1, dialogs.AccumulatedConfirmationCount);
        Assert.AreEqual(0, accumulator.Count);
    }

    [TestMethod]
    public async Task Accumulation_SendInProgress_BlocksDoubleClickDiscardAndExecution()
    {
        var (vm, accumulator, execution, _, _) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        execution.PendingMail = new TaskCompletionSource<OutlookDeliveryResult>();
        Task sending = vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.IsRunning);
        Assert.IsFalse(vm.CanDiscardAccumulated);
        Assert.IsFalse(vm.CanStartAutomation);
        Assert.IsFalse(vm.OpenCollectionsCommand.CanExecute(null));
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        await vm.StartAutomationCommand.ExecuteAsync(null);
        vm.DiscardAccumulatedCommand.Execute(null);
        Assert.AreEqual(1, execution.EmailSendCount);
        Assert.AreEqual(1, execution.RealExecutionCount);
        Assert.AreEqual(2, accumulator.Count);
        execution.PendingMail.SetResult(OutlookDeliveryResult.SentConfirmed());
        await sending;
        Assert.AreEqual(0, accumulator.Count);
    }

    [TestMethod]
    public async Task Accumulation_CloseAndDiscard_RequireExplicitConfirmation_SystemShutdownDoesNotWait()
    {
        var (vm, accumulator, execution, dialogs, _) = await CreateAccumulationAsync();
        await vm.StartAutomationCommand.ExecuteAsync(null);
        dialogs.ConfirmAccumulated = false;
        vm.DiscardAccumulatedCommand.Execute(null);
        Assert.IsFalse(vm.TryClose());
        Assert.AreEqual(2, accumulator.Count);
        Assert.IsTrue(vm.TryClose(systemShutdown: true));
        Assert.AreEqual(0, execution.EmailSendCount);
        dialogs.ConfirmAccumulated = true;
        Assert.IsTrue(vm.TryClose());
        Assert.AreEqual(0, accumulator.Count);
        Assert.IsNull(accumulator.CollectionId);
        Assert.IsTrue(new MainWindowViewModel().TryClose());
    }

    [TestMethod]
    public async Task Accumulation_EmptyBoundCollection_CanFixMailAndSendButCannotRun()
    {
        var (vm, accumulator, execution, _, collections) = await CreateAccumulationAsync(false);
        await vm.StartAutomationCommand.ExecuteAsync(null);
        FavoriteCollection old = collections.State.ActiveCollection!;
        CollectionMailSettings configured = Core.Outlook.CollectionMailComposerTests.Collection("Mail", "fixed").Mail!;
        collections.State = new FavoriteCollectionsState(
            [new FavoriteCollection(old.Id, old.Name, old.RequestReason, [], mail: configured)], old.Id);
        await vm.OpenCollectionsCommand.ExecuteAsync(null);
        Assert.IsFalse(vm.CanStartAutomation);
        await vm.StartAutomationCommand.ExecuteAsync(null);
        Assert.AreEqual(1, execution.RealExecutionCount);
        Assert.IsTrue(vm.CanSendAccumulatedMail);
        await vm.SendAccumulatedMailCommand.ExecuteAsync(null);
        Assert.AreEqual(1, execution.EmailSendCount);
        Assert.AreEqual("fixed@example.com", execution.LastAccumulatedMail!.ToRecipients[0]);
        Assert.AreEqual(0, accumulator.Count);
    }
}
